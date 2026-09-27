using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// File-backed repository for market quotes, storing at most one quote per symbol per day.
/// </summary>
public class JsonMarketQuoteRepository : IMarketQuoteRepository
{
    private readonly string _filePath;
    private readonly IVaultService? _vaultService;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMarketQuoteRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    /// <param name="vaultService">Optional vault service for encryption.</param>
    public JsonMarketQuoteRepository(string filePath, IVaultService? vaultService = null)
    {
        this._filePath = filePath;
        this._vaultService = vaultService;
    }

    /// <summary>
    /// Gets the most recent quote stored for a symbol, or null when the symbol never had a quote.
    /// </summary>
    /// <param name="symbol">The symbol/ticker to look up.</param>
    /// <returns>The latest stored quote, or null.</returns>
    public MarketQuote? GetLatest(string symbol)
    {
        string normalized = symbol?.Trim().ToUpperInvariant() ?? string.Empty;

        return this.GetAll()
            .Where(q => string.Equals(q.Symbol, normalized, StringComparison.Ordinal))
            .OrderByDescending(q => q.Date)
            .ThenByDescending(q => q.RetrievedAtUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Stores a quote, replacing any existing quote for the same symbol on the same day.
    /// Serialized so concurrent writes from the throttled price service cannot lose each other.
    /// </summary>
    /// <param name="quote">The quote to store.</param>
    public void Upsert(MarketQuote quote)
    {
        lock (this._lock)
        {
            string normalized = quote.Symbol;
            List<MarketQuote> quotes = this.GetAll().ToList();
            quotes.RemoveAll(q => string.Equals(q.Symbol, normalized, StringComparison.Ordinal) && q.Date == quote.Date);
            quotes.Add(quote);
            this.WriteAll(quotes);
        }
    }

    /// <summary>
    /// Gets all stored quotes, deduplicating by symbol + day (keeping the newest) in case the
    /// file ever contains duplicates.
    /// </summary>
    /// <returns>All stored quotes.</returns>
    public IReadOnlyList<MarketQuote> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this._filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<MarketQuote>();
        }

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        try
        {
            List<MarketQuote>? quotes = JsonSerializer.Deserialize<List<MarketQuote>>(json, options);
            if (quotes is null || quotes.Count == 0)
            {
                return new List<MarketQuote>();
            }

            List<MarketQuote> deduplicated = quotes
                .GroupBy(q => new { q.Symbol, q.Date })
                .Select(g => g.OrderByDescending(q => q.RetrievedAtUtc).First())
                .ToList();

            if (deduplicated.Count != quotes.Count)
            {
                this.WriteAll(deduplicated);
            }

            return deduplicated;
        }
        catch (JsonException)
        {
            int lastBrace = json.LastIndexOf('}');
            if (lastBrace > 0)
            {
                string repaired = json[.. (lastBrace + 1)] + "]";
                try
                {
                    List<MarketQuote>? recovered = JsonSerializer.Deserialize<List<MarketQuote>>(repaired, options);
                    if (recovered is not null)
                    {
                        this.WriteAll(recovered);
                        return recovered;
                    }
                }
                catch
                {
                }
            }

            return new List<MarketQuote>();
        }
    }

    private void WriteAll(IEnumerable<MarketQuote> quotes)
    {
        JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        string json = JsonSerializer.Serialize(quotes, options);
        EncryptedJsonFileStorage.WriteText(this._filePath, json, this._vaultService);
    }
}
using System.Globalization;
using System.Net;
using System.Text.Json;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Services;

/// <summary>
/// Provides currency conversion rates from the free Frankfurter API (no API key, no monthly quota).
/// </summary>
public class FrankfurterCurrencyConverter : ICurrencyConverter
{
    private const string DefaultBaseUrl = "https://api.frankfurter.dev";

    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyCollection<string> _excludedCurrencies;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterCurrencyConverter"/> class.
    /// </summary>
    /// <param name="httpClient">Optional HTTP client; a new one is created if not provided.</param>
    /// <param name="excludedCurrencies">Optional list of currency codes to exclude from requests (BTC is always excluded).</param>
    /// <param name="baseUrl">Optional base URL of the Frankfurter API.</param>
    public FrankfurterCurrencyConverter(HttpClient? httpClient = null, IReadOnlyCollection<string>? excludedCurrencies = null, string baseUrl = DefaultBaseUrl)
    {
        this._httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        this._excludedCurrencies = (excludedCurrencies ?? Array.Empty<string>())
            .Append("BTC")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        this._baseUrl = baseUrl.TrimEnd('/');
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string url = $"{this._baseUrl}/v2/rates?date={dateString}&base={source}&quotes={this.GetTargetCurrencies(source)}";

        IReadOnlyList<FrankfurterRateRecord> records = await this.GetRecordsAsync(url);

        Dictionary<string, decimal> result = new(StringComparer.Ordinal);

        foreach (FrankfurterRateRecord record in records)
        {
            if (record.Quote == source.ToString())
            {
                continue;
            }

            result[$"{source}{record.Quote}"] = record.Rate;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
        Currencies source,
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<Currencies>? targets = null,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
        {
            throw new ArgumentException("The end date must be greater than or equal to the start date.", nameof(end));
        }

        string quotes = string.Join(",", (targets ?? Enum.GetValues<Currencies>()).Where(currency => currency != source && this.IsRequested(currency)));
        string url = $"{this._baseUrl}/v2/rates?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}&base={source}&quotes={quotes}";

        IReadOnlyList<FrankfurterRateRecord> records = await this.GetRecordsAsync(url, cancellationToken);

        Dictionary<DateOnly, Dictionary<string, decimal>> result = new();

        foreach (IGrouping<string, FrankfurterRateRecord> group in records
            .Where(record => record.Quote != source.ToString())
            .GroupBy(r => r.Date, StringComparer.Ordinal))
        {
            if (DateOnly.TryParse(group.Key, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                Dictionary<string, decimal> quotesByPair = new(StringComparer.Ordinal);

                foreach (FrankfurterRateRecord record in group)
                {
                    quotesByPair[$"{source}{record.Quote}"] = record.Rate;
                }

                result[date] = quotesByPair;
            }
        }

        return result;
    }

    private string GetTargetCurrencies(Currencies source)
    {
        return string.Join(",", Enum.GetValues<Currencies>().Where(currency => currency != source && this.IsRequested(currency)));
    }

    private bool IsRequested(Currencies currency)
    {
        return !this._excludedCurrencies.Contains(currency.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<FrankfurterRateRecord>> GetRecordsAsync(string url, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await this._httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new CurrencyApiQuotaExceededException("Frankfurter API returned HTTP 429 (too many requests).");
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            string message = ExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
            throw new Exception($"Error in API response: {message}");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<FrankfurterRateRecord>>(json) ?? new List<FrankfurterRateRecord>();
    }
}

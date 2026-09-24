using System.Globalization;
using System.Net;
using System.Text.Json;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Http.Currency;

/// <summary>
/// Provides Bitcoin conversion rates from the CoinGecko API (free tier, no API key).
/// CoinGecko only covers crypto assets, so this converter is dedicated to BTC while
/// fiat pairs continue to be served by the fiat provider.
/// </summary>
public class CoinGeckoCurrencyConverter : ICurrencyConverter
{
    private const string DefaultBaseUrl = "https://api.coingecko.com/api/v3";
    private const string DefaultCoinId = "bitcoin";

    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("status", out JsonElement status) &&
                status.TryGetProperty("error_message", out JsonElement message) &&
                message.ValueKind == JsonValueKind.String)
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
    private readonly string _coinId;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinGeckoCurrencyConverter"/> class.
    /// </summary>
    /// <param name="httpClient">Optional HTTP client; a new one is created if not provided.</param>
    /// <param name="coinId">Optional CoinGecko coin identifier (defaults to "bitcoin").</param>
    /// <param name="baseUrl">Optional base URL of the CoinGecko API.</param>
    public CoinGeckoCurrencyConverter(HttpClient? httpClient = null, string coinId = DefaultCoinId, string baseUrl = DefaultBaseUrl)
    {
        this._httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        this._coinId = coinId;
        this._baseUrl = baseUrl;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        string dateString = date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        string url = $"{this._baseUrl}/coins/{this._coinId}/history?date={dateString}&localization=false";

        string json = await this.GetJsonAsync(url);
        CoinGeckoHistoryResponse? response = JsonSerializer.Deserialize<CoinGeckoHistoryResponse>(json);

        if (response is null ||
            !response.MarketData.CurrentPrice.TryGetValue(source.ToString().ToLowerInvariant(), out decimal priceInSource) ||
            priceInSource <= 0)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        return new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [$"{source}BTC"] = 1m / priceInSource,
        };
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

        if (targets != null && !targets.Contains(Currencies.BTC))
        {
            return new Dictionary<DateOnly, Dictionary<string, decimal>>();
        }

        long from = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        long to = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        string url = $"{this._baseUrl}/coins/{this._coinId}/market_chart/range?vs_currency={source.ToString().ToLowerInvariant()}&from={from}&to={to}";

        string json = await this.GetJsonAsync(url, cancellationToken);
        CoinGeckoMarketChartResponse? response = JsonSerializer.Deserialize<CoinGeckoMarketChartResponse>(json);

        Dictionary<DateOnly, Dictionary<string, decimal>> result = new();

        if (response is null)
        {
            return result;
        }

        foreach (IGrouping<DateOnly, double[]> group in response.Prices
            .Where(point => point.Length == 2 && point[1] > 0)
            .GroupBy(point => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds((long)point[0]).UtcDateTime)))
        {
            double[] lastPoint = group.OrderBy(point => point[0]).Last();
            decimal priceInSource = (decimal)lastPoint[1];

            if (priceInSource <= 0)
            {
                continue;
            }

            result[group.Key] = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [$"{source}BTC"] = 1m / priceInSource,
            };
        }

        return result;
    }

    private async Task<string> GetJsonAsync(string url, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await this._httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new CurrencyApiQuotaExceededException("CoinGecko API returned HTTP 429 (rate limit exceeded).");
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            string message = ExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
            throw new Exception($"Error in API response: {message}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
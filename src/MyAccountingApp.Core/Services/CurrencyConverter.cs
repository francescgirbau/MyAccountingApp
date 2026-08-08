using System.Text.Json;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Services;

/// <summary>
/// Provides currency conversion rates by fetching data from an external API.
/// </summary>
public class CurrencyConverter : ICurrencyConverter
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyCollection<string> _excludedCurrencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyConverter"/> class.
    /// </summary>
    /// <param name="apiKey">The API key for the external service.</param>
    /// <param name="httpClient">Optional HTTP client; a new one is created if not provided.</param>
    /// <param name="excludedCurrencies">Optional list of currency codes to exclude from requests.</param>
    public CurrencyConverter(string apiKey, HttpClient? httpClient = null, IReadOnlyCollection<string>? excludedCurrencies = null)
    {
        this._apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        this._httpClient = httpClient ?? new HttpClient();
        this._excludedCurrencies = excludedCurrencies ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        string currencyList = string.Join(",", this.GetTargetCurrencies(source).Select(c => c.ToString()));

        string url = $"https://api.exchangerate.host/historical?access_key={this._apiKey}&date={dateString}&source={source}&currencies={currencyList}";

        HttpResponseMessage response = await this.GetAsync(url);
        string json = await response.Content.ReadAsStringAsync();
        ExchangeRateResponse? result = JsonSerializer.Deserialize<ExchangeRateResponse>(json);

        ThrowIfFailed(result?.Success == true, result?.Error);

        return result!.Quotes;
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

        string currencyList = string.Join(",", (targets ?? this.GetTargetCurrencies(source)).Select(c => c.ToString()));

        string url = $"https://api.exchangerate.host/timeseries?access_key={this._apiKey}&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}&source={source}&currencies={currencyList}";

        HttpResponseMessage response = await this.GetAsync(url, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ExchangeRateTimeseriesResponse? result = JsonSerializer.Deserialize<ExchangeRateTimeseriesResponse>(json);

        ThrowIfFailed(result?.Success == true, result?.Error);

        Dictionary<DateOnly, Dictionary<string, decimal>> mapped = new();

        foreach (KeyValuePair<string, Dictionary<string, decimal>> entry in result!.Rates)
        {
            if (DateOnly.TryParse(entry.Key, out DateOnly date))
            {
                Dictionary<string, decimal> quotes = entry.Value.ToDictionary(
                    kv => $"{source}{kv.Key}",
                    kv => kv.Value,
                    StringComparer.Ordinal);
                mapped[date] = quotes;
            }
        }

        return mapped;
    }

    private IReadOnlyCollection<Currencies> GetTargetCurrencies(Currencies source)
    {
        return Enum.GetValues<Currencies>()
            .Where(c => c != source && !this._excludedCurrencies.Contains(c.ToString(), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await this._httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new CurrencyApiQuotaExceededException("Currency API returned HTTP 429 (too many requests).");
        }

        response.EnsureSuccessStatusCode();
        return response;
    }

    private static void ThrowIfFailed(bool success, ExchangeRateResponseError? error)
    {
        if (!success)
        {
            if (error?.Code == 104 || (error?.Info is not null && error.Info.Contains("limit", StringComparison.OrdinalIgnoreCase)))
            {
                throw new CurrencyApiQuotaExceededException($"Currency API quota exceeded: {error?.Info}");
            }

            throw new Exception($"Error in API response: {error?.Info}");
        }
    }
}

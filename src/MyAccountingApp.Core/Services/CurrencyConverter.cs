using System.Text.Json;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Services;

/// <summary>
/// Provides currency conversion rates by fetching data from an external API.
/// </summary>
public class CurrencyConverter : ICurrencyConverter
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public CurrencyConverter(string apiKey, HttpClient? httpClient = null)
    {
        this._apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        this._httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Asynchronously fetches conversion rates for all supported currencies based on the specified source currency and date.
    /// </summary>
    /// <param name="source">The base currency for conversion.</param>
    /// <param name="date">The date for which to fetch conversion rates.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary
    /// mapping currency pair codes (e.g., "EURUSD") to their conversion rates.
    /// </returns>
    /// <exception cref="Exception">Thrown if the API response is invalid or unsuccessful.</exception>
    public async Task<Dictionary<string, double>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        string currencyList = string.Join(",", Enum.GetValues<Currencies>().Where(c => c != source).Select(c => c.ToString()));

        string url = $"https://api.exchangerate.host/historical?access_key={_apiKey}&date={dateString}&source={source}&currencies={currencyList}";

        HttpResponseMessage response = await this._httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        ExchangeRateResponse? result = JsonSerializer.Deserialize<ExchangeRateResponse>(json);

        if (result == null || !result.Success)
        {
            throw new Exception($"Error in API response: {result?.Error?.Info}");
        }

        return result.Quotes;
    }
}

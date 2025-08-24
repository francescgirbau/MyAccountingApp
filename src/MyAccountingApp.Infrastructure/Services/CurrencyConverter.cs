using System.Text.Json;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Infrastructure.DTOs;

namespace MyAccountingApp.Infrastructure.Services;

/// <summary>
/// Provides currency conversion rates by fetching data from an external API.
/// </summary>
public class CurrencyConverter : ICurrencyConverter
{
    /// <summary>
    /// The API key used for authentication with the external currency rate service.
    /// </summary>
    private const string API_KEY = "3038e2941e7364716db9169d95d531"; // ToDo : Move to configuration

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyConverter"/> class.
    /// </summary>
    /// <param name="httpClient">Optional HTTP client for making API requests. If not provided, a new instance is created.</param>
    public CurrencyConverter(HttpClient? httpClient = null)
    {
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

        string url = $"https://api.exchangerate.host/historical?access_key={API_KEY}cd&date={dateString}&source={source}&currencies={currencyList}";

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

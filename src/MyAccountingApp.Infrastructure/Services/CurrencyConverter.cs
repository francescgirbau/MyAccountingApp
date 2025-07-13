using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Core.ValueObjects;
using MyAccountingApp.Infrastructure.DTOs;
using System;
using System.Text.Json;

namespace MyAccountingApp.Infrastructure.Services;

public class CurrencyConverter : ICurrencyConverter
{
    private readonly HttpClient _httpClient;
    private const string API_KEY = "3038e2941e7364716db9169d95d531";

    public CurrencyConverter(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Money> ConvertToAsync(Money original, Currencies targetCurrency, DateTime date)
    {
        if (original.Currency == targetCurrency)
            return original;

        string dateString = date.ToString("yyyy-MM-dd");

        string url = $"https://api.exchangerate.host/historical?access_key={API_KEY}cd&date={dateString}&source={original.Currency}&currencies={targetCurrency}";

        HttpResponseMessage response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        ExchangeRateResponse? result = JsonSerializer.Deserialize<ExchangeRateResponse>(json);

        if (result is null)
            throw new Exception("Resposta nul·la de l'API de tipus de canvi.");

        if (!result.Success)
        {
            if(result.Error is null)
                throw new Exception("L'API ha retornat un error de consulta.");


            throw new Exception($"L'API ha retornat un error {result.Error.Type} amb code {result.Error.Code} de consulta. {result.Error.Info}");


        }


        if (!result.Quotes.TryGetValue(targetCurrency.ToString(), out var rate))
            throw new Exception($"No s'ha trobat el tipus de canvi per {targetCurrency}.");

        var convertedAmount = original.Amount * rate;
        return new Money { Amount = convertedAmount, Currency = targetCurrency };
    }

    public async Task<Dictionary<string, double>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        string currencyList = string.Join(",", Enum.GetValues<Currencies>().Where(c => c != source).Select(c => c.ToString()));

        string url = $"https://api.exchangerate.host/historical?access_key={API_KEY}cd&date={dateString}&source={source}&currencies={currencyList}";

        HttpResponseMessage response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        ExchangeRateResponse? result = JsonSerializer.Deserialize<ExchangeRateResponse>(json);

        if (result == null || !result.Success)
            throw new Exception($"Error en la resposta de l'API: {result?.Error?.Info}");

        return result.Quotes;
    }
}

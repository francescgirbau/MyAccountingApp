using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents the response from the currency exchange rate API.
/// </summary>
public class ExchangeRateResponse
{
    /// <summary>
    /// Gets a value indicating whether the API request was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; } = false;

    /// <summary>
    /// Gets the dictionary of currency pair codes and their conversion rates.
    /// </summary>
    [JsonPropertyName("quotes")]
    public Dictionary<string, double> Quotes { get; init; } = new();

    /// <summary>
    /// Gets the error details if the API request was not successful.
    /// </summary>
    [JsonPropertyName("error")]
    public ExchangeRateResponseError? Error { get; init; } = null;
}

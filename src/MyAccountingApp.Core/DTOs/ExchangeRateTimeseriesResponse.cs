using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents the response from the currency exchange rate API timeseries endpoint.
/// </summary>
public class ExchangeRateTimeseriesResponse
{
    /// <summary>
    /// Gets a value indicating whether the API request was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; } = false;

    /// <summary>
    /// Gets the dictionary of dates and their currency pair rates.
    /// </summary>
    [JsonPropertyName("rates")]
    public Dictionary<string, Dictionary<string, decimal>> Rates { get; init; } = new();

    /// <summary>
    /// Gets the error details if the API request was not successful.
    /// </summary>
    [JsonPropertyName("error")]
    public ExchangeRateResponseError? Error { get; init; } = null;
}

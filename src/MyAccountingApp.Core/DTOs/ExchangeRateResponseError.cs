using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents error details returned by the currency exchange rate API.
/// </summary>
public class ExchangeRateResponseError
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; } = 0;

    /// <summary>
    /// Gets the type of error.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets additional information about the error.
    /// </summary>
    [JsonPropertyName("info")]
    public string Info { get; init; } = string.Empty;
}

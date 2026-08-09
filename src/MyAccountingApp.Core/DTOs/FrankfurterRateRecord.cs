using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents a single rate record returned by the Frankfurter API v2.
/// </summary>
public class FrankfurterRateRecord
{
    /// <summary>
    /// Gets the date of the rate (yyyy-MM-dd).
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base currency.
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;

    /// <summary>
    /// Gets the quote (target) currency.
    /// </summary>
    [JsonPropertyName("quote")]
    public string Quote { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exchange rate (units of quote per unit of base).
    /// </summary>
    [JsonPropertyName("rate")]
    public decimal Rate { get; init; }
}

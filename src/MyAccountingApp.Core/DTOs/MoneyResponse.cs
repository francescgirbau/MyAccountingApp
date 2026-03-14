using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class MoneyResponse
{
    /// <summary>
    /// Gets the amount of money.
    /// </summary>
    [JsonPropertyName("amount")]
    required public double Amount { get; init; }

    /// <summary>
    /// Gets the currency of the money.
    /// </summary>
    [JsonPropertyName("currency")]
    required public string Currency { get; init; }
}

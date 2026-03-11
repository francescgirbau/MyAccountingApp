using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class MoneyResponse
{
    /// <summary>
    /// Gets the amount of money.
    /// </summary>
    [JsonPropertyName("amount")]
    public double Amount { get; private set; }

    /// <summary>
    /// Gets the currency of the money.
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; private set; }
}

using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class MoneyResponse
{
    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

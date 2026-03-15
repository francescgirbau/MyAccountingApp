using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class AssetTransactionResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

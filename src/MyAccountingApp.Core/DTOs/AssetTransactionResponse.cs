using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class AssetTransactionResponse
{
    [JsonPropertyName("date")]
    required public string Date { get; init; }

    [JsonPropertyName("description")]
    required public string Description { get; init; }

    [JsonPropertyName("money")]
    required public MoneyResponse Money { get; init; }

    [JsonPropertyName("assetName")]
    required public string AssetName { get; init; }

    [JsonPropertyName("quantity")]
    required public double Quantity { get; init; }

    [JsonPropertyName("type")]
    required public string Type { get; init; }
}

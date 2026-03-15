using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class TransactionResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }
}

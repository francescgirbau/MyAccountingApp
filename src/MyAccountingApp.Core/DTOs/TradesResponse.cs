using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

public class TradesResponse
{
    [JsonPropertyName("trades")]
    public List<TradeResponse>? Trades { get; set; }

    [JsonPropertyName("dipòsits")]
    public List<DipositResponse>? Diposits { get; set; }

    [JsonPropertyName("dividends")]
    public List<DividendResponse>? Dividends { get; set; }

    [JsonPropertyName("others")]
    public List<OtherResponse>? Others { get; set; }
}

public class TradeResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }
}

public class DipositResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }
}

public class DividendResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }
}

public class OtherResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("money")]
    public MoneyResponse? Money { get; init; }
}

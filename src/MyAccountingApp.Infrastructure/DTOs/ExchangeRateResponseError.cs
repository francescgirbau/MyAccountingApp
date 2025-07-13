using System.Text.Json.Serialization;

namespace MyAccountingApp.Infrastructure.DTOs;

public class ExchangeRateResponseError
{

    [JsonPropertyName("code")]
    public int Code { get;init; } = 0;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("info")]
    public string Info { get; init; } = string.Empty;


}


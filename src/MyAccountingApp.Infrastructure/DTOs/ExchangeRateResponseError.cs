using System.Text.Json.Serialization;

namespace MyAccountingApp.Infrastructure.DTOs;

public class ExchangeRateResponseError
{

    [JsonPropertyName("code")]
    public int Code { get;init; }

    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("info")]
    public string Info { get; init; }


}


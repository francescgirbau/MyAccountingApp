using System.Text.Json.Serialization;

namespace MyAccountingApp.Infrastructure.DTOs;

public class ExchangeRateResponse
{

    [JsonPropertyName("success")]
    public bool Success { get; init; } = false;

    [JsonPropertyName("quotes")]
    public Dictionary<string, double> Quotes { get; init; } = new();

    [JsonPropertyName("error")]
    public ExchangeRateResponseError? Error { get; init; } = null;


}
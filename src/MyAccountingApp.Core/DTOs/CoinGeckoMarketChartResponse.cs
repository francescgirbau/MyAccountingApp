using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents the response of the CoinGecko market chart range endpoint.
/// </summary>
public class CoinGeckoMarketChartResponse
{
    /// <summary>
    /// Gets the price points as a list of (unix timestamp in milliseconds, price) pairs.
    /// </summary>
    [JsonPropertyName("prices")]
    public List<double[]> Prices { get; init; } = new();
}
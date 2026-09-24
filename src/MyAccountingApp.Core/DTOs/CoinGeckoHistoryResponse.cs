using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;

/// <summary>
/// Represents the response of the CoinGecko coin history endpoint.
/// </summary>
public class CoinGeckoHistoryResponse
{
    /// <summary>
    /// Gets the market data section of the response.
    /// </summary>
    [JsonPropertyName("market_data")]
    public CoinGeckoMarketData MarketData { get; init; } = new();
}

/// <summary>
/// Represents the market data section of a CoinGecko history response.
/// </summary>
public class CoinGeckoMarketData
{
    /// <summary>
    /// Gets the current price of the coin expressed in each quoted currency.
    /// </summary>
    [JsonPropertyName("current_price")]
    public Dictionary<string, decimal> CurrentPrice { get; init; } = new();
}
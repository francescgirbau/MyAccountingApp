using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class AssetTransactionResponse
{
    /// <summary>
    /// The associated financial transaction.
    /// </summary>
    [JsonPropertyName("transaction")]
    public TransactionResponse Transaction { get; private set; }

    /// <summary>
    /// The asset symbol (e.g., stock ticker).
    /// </summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; private set; }

    /// <summary>
    /// The quantity of the asset involved in the transaction.
    /// </summary>
    [JsonPropertyName("quantity")]
    public double Quantity { get; private set; }

    /// <summary>
    /// The type of asset transaction (buy, sell, dividend, tax withholding).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; private set; }
}

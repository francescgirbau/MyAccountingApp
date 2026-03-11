using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class TransactionResponse
{
    /// <summary>
    /// Gets the date of the transaction.
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; private set; }

    /// <summary>
    /// Gets the description of the transaction.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; private set; }

    /// <summary>
    /// Gets the monetary value of the transaction.
    /// </summary>
    [JsonPropertyName("money")]
    public MoneyResponse Money { get; private set; }

    /// <summary>
    /// Gets the category of the transaction (expense, income, or transfer).
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; private set; }
}

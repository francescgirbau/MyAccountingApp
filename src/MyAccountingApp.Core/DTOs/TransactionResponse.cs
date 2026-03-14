using System.Text.Json.Serialization;

namespace MyAccountingApp.Core.DTOs;
public class TransactionResponse
{
    /// <summary>
    /// Gets the date of the transaction.
    /// </summary>
    [JsonPropertyName("date")]
    required public string Date { get; init; }

    /// <summary>
    /// Gets the description of the transaction.
    /// </summary>
    [JsonPropertyName("description")]
    required public string Description { get; init; }

    /// <summary>
    /// Gets the monetary value of the transaction.
    /// </summary>
    [JsonPropertyName("money")]
    required public MoneyResponse Money { get; init; }

    /// <summary>
    /// Gets the category of the transaction (expense, income, or transfer).
    /// </summary>
    [JsonPropertyName("category")]
    required public string Category { get; init; }
}

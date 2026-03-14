namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Builds prompts for parsing Interactive Brokers CSV files.
/// </summary>
public interface IInteractiveBrokersPromptBuilder
{
    /// <summary>
    /// Builds a prompt to extract all transactions (both regular and asset) from a CSV file.
    /// </summary>
    /// <param name="csvContent">The full CSV file content.</param>
    /// <returns>The prompt text to send to the model.</returns>
    string BuildAllTransactionsPrompt(string csvContent);

}

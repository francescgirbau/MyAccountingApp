namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Builds prompts for parsing Interactive Brokers CSV files.
/// </summary>
public interface IInteractiveBrokersPromptBuilder
{
    /// <summary>
    /// Builds a prompt to extract regular transactions from a CSV file.
    /// </summary>
    /// <param name="csvContent">The full CSV file content.</param>
    /// <returns>The prompt text to send to the model.</returns>
    string BuildTransactionsPrompt(string csvContent);

    /// <summary>
    /// Builds a prompt to extract asset transactions from a CSV file.
    /// </summary>
    /// <param name="csvContent">The full CSV file content.</param>
    /// <returns>The prompt text to send to the model.</returns>
    string BuildAssetTransactionsPrompt(string csvContent);
}

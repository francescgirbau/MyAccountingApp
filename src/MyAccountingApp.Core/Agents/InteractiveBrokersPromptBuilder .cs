using System.Text;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Core.Agents;

public class InteractiveBrokersPromptBuilder : IInteractiveBrokersPromptBuilder
{
    /// <inheritdoc/>
    public string BuildTransactionsPrompt(string csvContent)
    {
        StringBuilder sb = new();

        sb.AppendLine("You are an assistant that extracts personal finance transactions from an Interactive Brokers CSV file.");
        sb.AppendLine("Return a JSON object with a property \"transactions\" which is an array.");
        sb.AppendLine("Each transaction must have: date (YYYY-MM-DD), description, money (amount, currency), category.");
        sb.AppendLine("Categories must match exactly the enum values used by MyAccountingApp: INCOME, EXPENSE, TRANSFER, DEPOSIT.");
        sb.AppendLine("Amounts for EXPENSE and TRANSFER must be negative. Amounts for INCOME and DEPOSIT must be positive.");
        sb.AppendLine("Please, do not take into account summar summation information, such as Total or Subtotal.");
        sb.AppendLine();
        sb.AppendLine("CSV content:");
        sb.AppendLine("```csv");
        sb.AppendLine(csvContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Now output only the JSON object, with no explanation text.");

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string BuildAssetTransactionsPrompt(string csvContent)
    {
        StringBuilder sb = new();

        sb.AppendLine("You are an assistant that extracts asset transactions from an Interactive Brokers CSV file.");
        sb.AppendLine("Return a JSON object with a property \"assetTransactions\" which is an array.");
        sb.AppendLine("Each asset transaction must have: date (YYYY-MM-DD), description, money (amount, currency), assetName, type.");
        sb.AppendLine("Types must match exactly the enum values used by MyAccountingApp: Buy, Sell, TaxWithholding, Dividend.");
        sb.AppendLine("For Buy and TaxWithholding the category must be EXPENSE. For Sell and Dividend the category must be INCOME.");
        sb.AppendLine();
        sb.AppendLine("CSV content:");
        sb.AppendLine("```csv");
        sb.AppendLine(csvContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Now output only the JSON object, with no explanation text.");

        return sb.ToString();
    }
}

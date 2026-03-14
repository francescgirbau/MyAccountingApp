using System.Text;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Core.Agents;

public class InteractiveBrokersPromptBuilder : IInteractiveBrokersPromptBuilder
{
    /// <inheritdoc/>
    public string BuildAllTransactionsPrompt(string csvContent)
    {
        StringBuilder sb = new();

        sb.AppendLine("You are an assistant that extracts personal finance transactions from an Interactive Brokers CSV file.");
        sb.AppendLine("Return a JSON object with TWO properties:");
        sb.AppendLine("- \"transactions\": for dividends, taxes, fees, and other non-asset movements");
        sb.AppendLine("- \"assetTransactions\": ONLY for Buy and Sell transactions (that change the number of shares you own)");
        sb.AppendLine();
        sb.AppendLine("For \"transactions\": date (YYYY-MM-DD), description, money (amount, currency), category.");
        sb.AppendLine("Categories: INCOME, EXPENSE, TRANSFER, DEPOSIT.");
        sb.AppendLine("Amounts for EXPENSE and TRANSFER must be negative. Amounts for INCOME and DEPOSIT must be positive.");
        sb.AppendLine("A TRANSFER and a DEPOSIT are internal money exchanges, for example from my bank account to my broker and vice versa");
        sb.AppendLine("An EXPENSE and an INCOME are external money exchanges, for example my costs or my salary");
        sb.AppendLine();
        sb.AppendLine("For \"assetTransactions\": date (YYYY-MM-DD), description, money (amount, currency), assetName, quantity, type.");
        sb.AppendLine("Types: Buy, Sell. ONLY include Buy and Sell here - NOT Dividend or TaxWithholding.");
        sb.AppendLine("For Buy: category must be EXPENSE (negative amount). For Sell: category must be INCOME (positive amount).");
        sb.AppendLine("Please, do not take into account summation information, such as Total or Subtotal.");
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

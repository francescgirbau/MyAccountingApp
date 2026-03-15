using System.Text;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Core.Agents;

public class InteractiveBrokersPromptBuilder : IInteractiveBrokersPromptBuilder
{
    public string BuildAllTransactionsPrompt(string csvContent)
    {
        StringBuilder sb = new();

        sb.AppendLine("Role: You are an AI assistant specialized in financial data extraction from Interactive Brokers CSV exports.");
        sb.AppendLine();
        sb.AppendLine("Task: Analyze the CSV and extract data into 4 separate arrays. Each row must go to exactly ONE array.");
        sb.AppendLine();
        sb.AppendLine("4 Tables to Extract:");
        sb.AppendLine();
        sb.AppendLine("1. 'trades': Buy and Sell orders of stocks/ETFs");
        sb.AppendLine("   - Look for: Symbol, Quantity, Proceeds columns");
        sb.AppendLine("   - Fields: date, symbol, quantity, money(amount,currency)");
        sb.AppendLine();
        sb.AppendLine("2. 'dipòsits': Money entering the account");
        sb.AppendLine("   - Description with 'Deposit', 'Transfer In', 'Wire In'");
        sb.AppendLine("   - Fields: date, description, money(amount,currency)");
        sb.AppendLine();
        sb.AppendLine("3. 'dividends': Dividend payments received");
        sb.AppendLine("   - Description with 'Dividend'");
        sb.AppendLine("   - Fields: date, description, money(amount,currency)");
        sb.AppendLine();
        sb.AppendLine("4. 'others': Fees, taxes, commissions, interest, and any other movement");
        sb.AppendLine("   - Fields: date, description, money(amount,currency)");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- date: YYYY-MM-DD format (extract only date, ignore time)");
        sb.AppendLine("- money: {amount: number, currency: string}");
        sb.AppendLine("- Ignore rows with 'Total', 'Subtotal', 'Summary', headers, or empty data");
        sb.AppendLine("- Interactive Brokers: negative = money OUT, positive = money IN");
        sb.AppendLine();
        sb.AppendLine("CSV content:");
        sb.AppendLine("```csv");
        sb.AppendLine(csvContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Output: Return ONLY JSON with: trades, dipòsits, dividends, others");

        return sb.ToString();
    }
}

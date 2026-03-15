using System.Text;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Core.Agents;

public class InteractiveBrokersPromptBuilder : IInteractiveBrokersPromptBuilder
{
    public string BuildAllTransactionsPrompt(string csvContent)
    {
        StringBuilder sb = new();

        sb.AppendLine("Role: You are an AI assistant specialized in financial data extraction. Your task is to process a CSV file exported from Interactive Brokers and extract specific types of financial transactions, returning them in a strictly formatted JSON structure.");
        sb.AppendLine();
        sb.AppendLine("Task: Analyze the provided CSV content line by line. For each row, determine if it belongs to one of two categories: 'transactions' or 'assetTransactions'. Crucially, each row must fit into exactly one category. Do not include any row in both categories, and do not include rows that fit into neither.");
        sb.AppendLine();
        sb.AppendLine("Category Definitions and Extraction Rules:");
        sb.AppendLine();
        sb.AppendLine("Category: 'transactions' (Dividends, Taxes, Fees, Deposits, Transfers, etc.)");
        sb.AppendLine();
        sb.AppendLine("What they are: These rows represent events that are not trades (buys/sells) but are financial events impacting your account balance or holdings (like income, expenses, deposits, withdrawals, transfers between accounts, fees, taxes, dividends).");
        sb.AppendLine();
        sb.AppendLine("Data Fields to Extract:");
        sb.AppendLine("- date: Extract the settlement date (Settle Date) or the statement date (WhenGenerated). If a date/time string is provided (e.g., '2022-05-23', '2022-05-23, 11:31:26 EDT'), extract ONLY the date part (YYYY-MM-DD format).");
        sb.AppendLine("- description: Extract the description field (Description). This is the text explaining the transaction (e.g., 'Electronic Fund Transfer', 'Dividend Payment', 'Fee: Trading Fee').");
        sb.AppendLine("- money: Extract the monetary amount and currency associated with the transaction. This should be an object with two properties: amount (a number, potentially negative) and currency (a string, e.g., 'EUR'). Carefully map this to the appropriate column (Amount, Proceeds, Comm/Fee, etc.) based on the description and context. If the amount is zero, exclude the row from this category.");
        sb.AppendLine("- category: Assign one of the following predefined strings based on the description and nature of the transaction:");
        sb.AppendLine("  - INCOME: For dividends (Dividend) or other income payments (e.g., interest).");
        sb.AppendLine("  - EXPENSE: For taxes (Tax), fees (Fee), commissions (Commission), or any other expense.");
        sb.AppendLine("  - TRANSFER: For transfers between accounts (e.g., 'Transfer to Main Account', 'Transfer from Sub-Account').");
        sb.AppendLine("  - DEPOSIT: For deposits into your account (e.g., 'Electronic Fund Transfer' if it's a credit).");
        sb.AppendLine();
        sb.AppendLine("Category: 'assetTransactions' (Buys and Sells of Assets)");
        sb.AppendLine();
        sb.AppendLine("What they are: These rows represent the buying or selling of financial assets (like stocks). They detail the trade execution.");
        sb.AppendLine();
        sb.AppendLine("Data Fields to Extract:");
        sb.AppendLine("- date: Extract the date of the trade execution (Date/Time). Extract ONLY the date part (YYYY-MM-DD format).");
        sb.AppendLine("- description: Extract the Symbol (e.g., 'ADSd', 'ALVd'). This is the core identifier for the asset traded.");
        sb.AppendLine("- money: Extract the monetary value associated with the trade settlement (Proceeds). The sign of the amount field will determine the type. The currency should be extracted from the Currency column.");
        sb.AppendLine("- symbol: Extract the stock ticker symbol (usually found in a column named Symbol, Security, or identified by the unique identifier like 'ADSd', 'ALVd').");
        sb.AppendLine("- quantity: Extract the quantity of the asset being traded. This is typically found in a column named Shares, Quantity, Volume.");
        sb.AppendLine("- type: Determine if the transaction is a Buy or Sell based on the quantity and price columns. 99% are times are Buys:");
        sb.AppendLine();
        sb.AppendLine("General Rules & Constraints:");
        sb.AppendLine();
        sb.AppendLine("- Row Classification: Every row must be evaluated and classified into either 'transactions' or 'assetTransactions' OR excluded entirely (if it doesn't fit either category, like header rows or rows with no data). Never include a row in both categories.");
        sb.AppendLine("- Date Format: Ensure the date field in JSON is always in the YYYY-MM-DD format.");
        sb.AppendLine("- Currency: Extract the currency explicitly from the data from a Currency column if available.");
        sb.AppendLine("- Excluding Totals/Subtotals: Explicitly ignore rows that contain text like 'Total', 'Subtotal', 'Summary', or are clearly header/footer rows.");
        sb.AppendLine("- JSON Output Structure: Return a JSON object with exactly two top-level keys: 'transactions' and 'assetTransactions'.");
        sb.AppendLine("- Each transaction object should have: date, description, money (object with amount and currency), category.");
        sb.AppendLine("- Each assetTransaction object should have: date, description, money (object with amount and currency), symbol, quantity, type.");
        sb.AppendLine("- Interactive Broker Financials have a clear sign convention, with negative values representing expenses, costs, buys, money moving out of the brokers account, while positive values mean incomes, paid interest, dividends  or sells, money moving into the broker account.");
        sb.AppendLine();
        sb.AppendLine("Input CSV:");
        sb.AppendLine("```csv");
        sb.AppendLine(csvContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Output: Return ONLY the JSON object. Do not include any introductory text, explanations, or trailing text.");

        return sb.ToString();
    }
}

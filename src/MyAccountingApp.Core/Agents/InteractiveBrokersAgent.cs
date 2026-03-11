using System.Net.Http.Json;
using System.Text.Json;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class InteractiveBrokersAgent : ITransactionAgent, IAssetTransactionAgent
{
    private const string BASE_ADDRESS = "http://localhost:11434/"; // Ollama server address
    private const string MODEL_NAME = "llama3"; // O el model que hagis baixat a Ollama
    private readonly HttpClient _httpClient;

    public InteractiveBrokersAgent(HttpClient? httpClient = null)
    {
        this._httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(BASE_ADDRESS) };
    }

    public async Task<IEnumerable<Transaction>> ParseTransactionsAsync(string filePath)
    {
        string csvContent = await File.ReadAllTextAsync(filePath);
        string prompt = BuildTransactionPrompt(csvContent);

        string jsonResponse = await this.CallOllamaAsync(prompt);

        JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
        };

        List<TransactionResponse>? transactionResponses = JsonSerializer.Deserialize<List<TransactionResponse>>(jsonResponse, options);

        if (transactionResponses != null)
        {
            List<Transaction> transactions = new List<Transaction>();
            foreach (TransactionResponse tr in transactionResponses)
            {
                if (DateTime.TryParse(tr.Date, out DateTime date) &&
                    !string.IsNullOrWhiteSpace(tr.Money.Currency) &&
                    Enum.TryParse(tr.Category, true, out TransactionCategory category))
                {
                    Money money = new Money(tr.Money.Amount, tr.Money.Currency);

                    Transaction transaction = new Transaction(date, tr.Description, money, category);

                    transactions.Add(transaction);
                }
            }

            return transactions;
        }

        return new List<Transaction>();
    }

    public async Task<IEnumerable<AssetTransaction>> ParseAssetTransactionsAsync(string filePath)
    {
        string csvContent = await File.ReadAllTextAsync(filePath);
        string prompt = BuildAssetTransactionPrompt(csvContent);

        string jsonResponse = await this.CallOllamaAsync(prompt);

        List<AssetTransactionResponse>? assetTransactionResponses = JsonSerializer.Deserialize<List<AssetTransactionResponse>>(jsonResponse);

        if (assetTransactionResponses != null)
        {
            List<AssetTransaction> assetTransactions = new List<AssetTransaction>();

            foreach (AssetTransactionResponse atr in assetTransactionResponses)
            {
                if (DateTime.TryParse(atr.Transaction.Date, out DateTime date) &&
                    !string.IsNullOrWhiteSpace(atr.Transaction.Money.Currency) &&
                    Enum.TryParse(atr.Transaction.Category, true, out TransactionCategory category) &&
                    Enum.TryParse(atr.Type, true, out AssetTransactionType type))
                {
                    Money money = new Money(atr.Transaction.Money.Amount, atr.Transaction.Money.Currency);
                    Transaction transaction = new Transaction(date, atr.Transaction.Description, money, category);
                    AssetTransaction assetTransaction = new AssetTransaction(transaction, atr.Symbol, atr.Quantity, type);
                    assetTransactions.Add(assetTransaction);
                }
            }

            return assetTransactions;
        }

        return new List<AssetTransaction>();
    }

    private async Task<string> CallOllamaAsync(string prompt)
    {
        var request = new
        {
            model = MODEL_NAME,
            prompt = prompt,
            stream = false,
        };

        using HttpResponseMessage response = await this._httpClient.PostAsJsonAsync("api/generate", request);
        response.EnsureSuccessStatusCode();

        OllamaResponse? result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

        if (result == null)
        {
            throw new InvalidOperationException("Invalid response from Ollama API.");
        }

        return result.GetJsonFromResponse();
    }

    private static string BuildTransactionPrompt(string csvContent)
    {
        return $@"
You are a financial data parser. 
Your task is to read the following Interactive Brokers CSV data and output a JSON array of 'TransactionResponse' objects.

Definition of a Transaction:
- A transaction is a movement of money NOT related to any asset.
- Each transaction has:
  - date: ISO 8601 format (YYYY-MM-DD)
  - description: text describing the transaction
  - money: object {{ amount: number, currency: string }}
  - category: one of ['EXPENSE', 'INCOME', 'TRANSFER', 'DEPOSIT'] no other values are allowed.
- Most transactions are DEPOSIT or TRANSFER because they come from or go to another owned account.
- Include as EXPENSE any commissions or payments done at account level NOT related to stocks or assets.
- Amounts:
  - Money coming IN must be positive (DEPOSIT or INCOME)
  - Money going OUT must be negative (EXPENSE or TRANSFER)
- IMPORTANT: Transactions that belong to this list must NOT appear in the AssetTransaction list.

CSV data:
{csvContent}

Output rules:
- Return ONLY valid JSON
- No explanations, no extra text
";
    }
    private static string BuildAssetTransactionPrompt(string csvContent)
    {
        return $@"
You are a financial data parser. 
Your task is to read the following Interactive Brokers CSV data and output a JSON array of 'AssetTransactionResponse' objects.

Definition of an AssetTransaction:
- An AssetTransaction is a transaction related to assets (stocks, ETFs, funds, bonds, etc.).
- Each AssetTransaction contains:
  - transaction: each transaction has:
      - date: ISO 8601 format (YYYY-MM-DD)
      - description: text describing the transaction
      - money: object {{ amount: number, currency: string }}
      - category: one of ['EXPENSE', 'INCOME'] no other values are allowed.
  - symbol: stock ticker or asset identifier
  - quantity: number (positive for buy, negative for sell)
  - type: one of ['BUY', 'SELL', 'DIVIDEND', 'TAX_WITHHOLDING']
- Include in this list any transactions related to the assets, such as:
  - Dividend payments
  - Asset-related taxes
  - Buys and sells
  - Corporate actions
- Amounts:
  - Money coming IN must be positive (income)
  - Money going OUT must be negative (expense)
- IMPORTANT: A transaction cannot appear in both the AssetTransaction list and the Transaction list.

CSV data:
{csvContent}

Output rules:
- Return ONLY valid JSON
- No explanations, no extra text
";
    }

}

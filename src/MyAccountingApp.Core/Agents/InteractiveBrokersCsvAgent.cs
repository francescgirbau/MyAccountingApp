namespace MyAccountingApp.Core.Agents;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Core.Models;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class InteractiveBrokersCsvAgent : IAgent
{
    private readonly ICsvParser csvParser;
    private readonly IOllamaClient ollamaClient;
    private readonly string modelName;
    private readonly int maxRetries;
    private readonly int initialDelayMs;
    private readonly ILogger<InteractiveBrokersCsvAgent> logger;

    public InteractiveBrokersCsvAgent(
        ICsvParser csvParser,
        IOllamaClient ollamaClient,
        string modelName,
        ILogger<InteractiveBrokersCsvAgent> logger,
        int maxRetries = 3,
        int initialDelayMs = 1000)
    {
        this.csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        this.ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        this.modelName = string.IsNullOrWhiteSpace(modelName)
            ? throw new ArgumentException("Model name must be provided.", nameof(modelName))
            : modelName;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.maxRetries = maxRetries;
        this.initialDelayMs = initialDelayMs;
    }

    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.logger.LogInformation("Parsing CSV file: {FilePath}", filePath);

        IEnumerable<IBKRTransactionRecord> records = await this.csvParser.ParseIBKRAsync(filePath);
        List<IBKRTransactionRecord> recordList = records.ToList();

        this.logger.LogInformation("Found {Count} CSV records", recordList.Count);

        List<Transaction> transactions = new List<Transaction>();
        List<AssetTransaction> assetTransactions = new List<AssetTransaction>();

        foreach (IBKRTransactionRecord record in recordList)
        {
            try
            {
                (bool isTrade, Transaction? transaction, AssetTransaction? assetTransaction) = await this.ClassifyAndMapAsync(record, cancellationToken);

                if (isTrade && assetTransaction != null)
                {
                    assetTransactions.Add(assetTransaction);
                }
                else if (!isTrade && transaction != null)
                {
                    transactions.Add(transaction);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to process record: {Description}", record.Description);
            }
        }

        this.logger.LogInformation("Parsed {TransactionCount} transactions and {AssetTransactionCount} asset transactions",
            transactions.Count, assetTransactions.Count);

        return (transactions, assetTransactions);
    }

    public async Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.logger.LogInformation("Parsing Corporate Actions CSV file: {FilePath}", filePath);

        IEnumerable<IBKRCorporateActionRecord> records = await this.csvParser.ParseCorporateActionsAsync(filePath);
        List<IBKRCorporateActionRecord> recordList = records.ToList();

        this.logger.LogInformation("Found {Count} Corporate Action records", recordList.Count);

        List<AssetTransaction> assetTransactions = new List<AssetTransaction>();

        foreach (IBKRCorporateActionRecord record in recordList)
        {
            try
            {
                AssetTransaction? assetTransaction = this.MapCorporateActionToAssetTransaction(record);
                if (assetTransaction != null)
                {
                    assetTransactions.Add(assetTransaction);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to process corporate action: {Description}", record.Description);
            }
        }

        this.logger.LogInformation("Parsed {Count} corporate action asset transactions", assetTransactions.Count);

        return assetTransactions;
    }

    private AssetTransaction? MapCorporateActionToAssetTransaction(IBKRCorporateActionRecord record)
    {
        double quantity = this.ParseAmount(record.Quantity ?? "0");
        double proceeds = this.ParseAmount(record.Proceeds ?? "0");
        string currency = record.Currency ?? "EUR";
        
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }
        
        string description = record.Description ?? string.Empty;
        
        string symbol = this.ExtractSymbolFromDescription(description);
        
        bool isSell = quantity < 0;
        
        TransactionCategory category = isSell ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
        AssetTransactionType type = isSell ? AssetTransactionType.Sell : AssetTransactionType.Buy;

        DateTime date = this.ParseDate(record.ReportDate);
        
        Transaction transaction = new Transaction(
            date,
            symbol,
            new Money(Math.Abs(proceeds), currency),
            category);

        return new AssetTransaction(
            transaction,
            symbol,
            Math.Abs(quantity),
            type);
    }

    private string ExtractSymbolFromDescription(string description)
    {
        try
        {
            int parenOpen = description.IndexOf('(');
            if (parenOpen > 0)
            {
                string symbol = description.Substring(0, parenOpen).Trim();
                int dotIndex = symbol.IndexOf('.');
                if (dotIndex > 0)
                {
                    symbol = symbol.Substring(0, dotIndex);
                }
                return symbol;
            }
        }
        catch
        {
        }
        
        return "UNKNOWN";
    }

    private async Task<(bool IsTrade, Transaction? Transaction, AssetTransaction? AssetTransaction)> ClassifyAndMapAsync(
        IBKRTransactionRecord record,
        CancellationToken cancellationToken)
    {
        string symbol = record.Symbol ?? string.Empty;
        string description = record.Description ?? string.Empty;
        string transactionType = record.TransactionType ?? string.Empty;
        
        bool isOption = this.IsOption(symbol) || this.IsOptionByDescription(description);
        
        if (isOption)
        {
            Transaction transaction = this.MapOptionToTransaction(record);
            return (false, transaction, null);
        }
        
        bool hasSymbol = !string.IsNullOrEmpty(symbol) && symbol != "-";
        
        if (hasSymbol)
        {
            AssetTransaction assetTransaction = this.MapToAssetTransaction(record);
            return (true, null, assetTransaction);
        }
        
        Transaction transaction2 = this.MapToTransaction(record);
        return (false, transaction2, null);
    }

    private Transaction MapOptionToTransaction(IBKRTransactionRecord record)
    {
        double amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";
        
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }
        
        string symbol = record.Symbol ?? "UNKNOWN";
        string underlyingSymbol = this.ExtractUnderlyingSymbol(symbol);
        
        double quantity = this.ParseAmount(record.Quantity ?? "0");
        bool isBuy = quantity > 0;
        
        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

        DateTime date = this.ParseDate(record.Date);

        return new Transaction(
            date,
            underlyingSymbol,
            new Money(Math.Abs(amount), currency),
            category);
    }

    private string ExtractUnderlyingSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            return "UNKNOWN";
        }
        
        int spaceIndex = symbol.IndexOf(' ');
        if (spaceIndex > 0)
        {
            return symbol.Substring(0, spaceIndex).Trim();
        }
        
        return symbol;
    }

    private async Task<AssetTransaction> MapCorporateActionAsync(
        IBKRTransactionRecord record,
        CancellationToken cancellationToken)
    {
        double amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }

        double quantity = this.CalculateCorporateActionQuantity(record.Description, amount, currency);
        
        string baseSymbol = await this.ExtractCorporateActionSymbolAsync(record, cancellationToken);
        
        TransactionCategory category = TransactionCategory.INCOME;
        AssetTransactionType type = AssetTransactionType.Sell;
        
        DateTime date = this.ParseDate(record.Date);
        
        Transaction transaction = new Transaction(
            date,
            baseSymbol,
            new Money(Math.Abs(amount), currency),
            category);
        
        return new AssetTransaction(
            transaction,
            baseSymbol,
            quantity,
            type);
    }

    private double CalculateCorporateActionQuantity(string description, double amount, string currency)
    {
        try
        {
            double pricePerShare = this.ExtractPricePerShare(description);
            if (pricePerShare > 0)
            {
                double calculatedQuantity = amount / pricePerShare;
                if (calculatedQuantity > 0 && calculatedQuantity < 10000)
                {
                    return Math.Round(calculatedQuantity, 2);
                }
            }
        }
        catch
        {
        }
        
        return 1;
    }

    private double ExtractPricePerShare(string description)
    {
        try
        {
            string upper = description.ToUpperInvariant();
            
            var match = System.Text.RegularExpressions.Regex.Match(
                upper, 
                @"(\d+[.,]?\d*)\s*(?:USD|CAD|EUR|GBP|AUD)\s*(?:PER\s*SHARE|PER\s*SH)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                string priceStr = match.Groups[1].Value.Replace(",", ".");
                if (double.TryParse(priceStr, System.Globalization.CultureInfo.InvariantCulture, out double price))
                {
                    return price;
                }
            }
            
            match = System.Text.RegularExpressions.Regex.Match(
                upper, 
                @"FOR\s+(\d+[.,]?\d*)\s*(?:USD|CAD|EUR|GBP|AUD)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                string priceStr = match.Groups[1].Value.Replace(",", ".");
                if (double.TryParse(priceStr, System.Globalization.CultureInfo.InvariantCulture, out double price))
                {
                    return price;
                }
            }
        }
        catch
        {
        }
        
        return 0;
    }

    private async Task<string> ExtractCorporateActionSymbolAsync(
        IBKRTransactionRecord record,
        CancellationToken cancellationToken)
    {
        string symbol = record.Symbol ?? "UNKNOWN";
        
        int dotIndex = symbol.IndexOf('.');
        if (dotIndex > 0)
        {
            return symbol.Substring(0, dotIndex);
        }
        
        return symbol;
    }

    private Transaction MapToTransaction(IBKRTransactionRecord record)
    {
        double amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";
        
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }

        TransactionCategory category = TransactionCategory.EXPENSE;

        string transactionType = (record.TransactionType ?? "").ToLower();
        string descLower = (record.Description ?? "").ToLower();
        
        if (transactionType.Contains("deposit") || descLower.Contains("deposit") || descLower.Contains("transfer"))
        {
            category = TransactionCategory.DEPOSIT;
        }
        else if (transactionType.Contains("withholding") || transactionType.Contains("tax"))
        {
            category = TransactionCategory.EXPENSE;
        }
        else if (transactionType.Contains("dividend") || descLower.Contains("dividend"))
        {
            category = TransactionCategory.INCOME;
        }
        else if (transactionType.Contains("credit interest") || descLower.Contains("credit interest"))
        {
            category = TransactionCategory.INCOME;
        }
        else if (transactionType.Contains("debit interest") || descLower.Contains("debit interest"))
        {
            category = TransactionCategory.EXPENSE;
        }
        else if (transactionType.Contains("fee") || descLower.Contains("fee"))
        {
            category = TransactionCategory.EXPENSE;
        }
        
        double quantity = this.ParseAmount(record.Quantity ?? "0");
        bool isOption = this.IsOption(record.Symbol ?? string.Empty) || this.IsOptionByDescription(descLower);
        
        if (isOption)
        {
            if (quantity > 0)
            {
                category = TransactionCategory.EXPENSE;
            }
            else
            {
                category = TransactionCategory.INCOME;
            }
        }

        DateTime date = this.ParseDate(record.Date);

        return new Transaction(
            date,
            record.Description ?? "Unknown",
            new Money(Math.Abs(amount), currency),
            category);
    }

    private AssetTransaction MapToAssetTransaction(IBKRTransactionRecord record)
    {
        double quantity = this.ParseAmount(record.Quantity ?? "0");
        double amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";
        
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }
        
        string symbol = record.Symbol ?? "UNKNOWN";
        string description = record.Description ?? string.Empty;
        string transactionType = record.TransactionType ?? string.Empty;
        
        bool isAssignment = description.Contains("Assignment", StringComparison.OrdinalIgnoreCase);
        bool isExercise = description.Contains("Exercise", StringComparison.OrdinalIgnoreCase);
        
        bool isBuy;
        AssetTransactionType type;
        
        if (isAssignment || isExercise)
        {
            bool isCall = description.Contains(" C ", StringComparison.OrdinalIgnoreCase) || 
                          description.EndsWith(" C", StringComparison.OrdinalIgnoreCase);
            isBuy = isCall;
            type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
        }
        else if (quantity == 0)
        {
            if (amount > 0)
            {
                isBuy = false;
                type = AssetTransactionType.Sell;
            }
            else
            {
                isBuy = true;
                type = AssetTransactionType.Buy;
            }
        }
        else
        {
            isBuy = quantity > 0;
            type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
        }
        
        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

        DateTime date = this.ParseDate(record.Date);

        Transaction transaction = new Transaction(
            date,
            symbol,
            new Money(Math.Abs(amount), currency),
            category);

        return new AssetTransaction(
            transaction,
            symbol,
            Math.Abs(quantity) > 0 ? Math.Abs(quantity) : 1,
            type);
    }

    private bool IsOption(string symbol)
    {
        return !string.IsNullOrEmpty(symbol) && symbol.Contains(" ") && (symbol.Contains("C") || symbol.Contains("P"));
    }

    private bool IsOptionByDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return false;
        }
        
        string upper = description.ToUpperInvariant();
        return upper.Contains(" C ") || upper.Contains(" P ") || 
               upper.EndsWith(" C") || upper.EndsWith(" P") ||
               upper.Contains("CALL") || upper.Contains("PUT");
    }

    private DateTime ParseDate(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return DateTime.Now;
        }

        string cleaned = dateStr.Split(',')[0].Trim();

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
        {
            return result;
        }

        return DateTime.Now;
    }

    private double ParseAmount(string amountStr)
    {
        if (string.IsNullOrEmpty(amountStr))
        {
            return 0;
        }

        amountStr = amountStr.Trim().Replace(",", "");

        if (double.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        return 0;
    }
}

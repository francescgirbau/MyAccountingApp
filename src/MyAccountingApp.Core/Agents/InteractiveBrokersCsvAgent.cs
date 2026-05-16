namespace MyAccountingApp.Core.Agents;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Core.Models;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class InteractiveBrokersCsvAgent : IAgent
{
    private readonly ICsvParser csvParser;
    private readonly ILogger<InteractiveBrokersCsvAgent> logger;

    public InteractiveBrokersCsvAgent(
        ICsvParser csvParser,
        ILogger<InteractiveBrokersCsvAgent> logger)
    {
        this.csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        this.logger.LogInformation(
            "Parsed {TransactionCount} transactions and {AssetTransactionCount} asset transactions",
            transactions.Count,
            assetTransactions.Count);

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
        decimal quantity = this.ParseAmount(record.Quantity ?? "0");
        decimal proceeds = this.ParseAmount(record.Proceeds ?? "0");
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
            new Money(proceeds < 0 ? -proceeds : proceeds, currency),
            category);

        return new AssetTransaction(
            transaction,
            symbol,
            quantity < 0 ? -quantity : quantity,
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
        decimal amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";

        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }

        string symbol = record.Symbol ?? "UNKNOWN";
        string underlyingSymbol = this.ExtractUnderlyingSymbol(symbol);

        decimal quantity = this.ParseAmount(record.Quantity ?? "0");
        bool isBuy = quantity > 0;

        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

        DateTime date = this.ParseDate(record.Date);

        return new Transaction(
            date,
            underlyingSymbol,
            new Money(amount < 0 ? -amount : amount, currency),
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
        decimal amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";
        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }

        decimal quantity = this.CalculateCorporateActionQuantity(record.Description, amount, currency);

        string baseSymbol = await this.ExtractCorporateActionSymbolAsync(record, cancellationToken);

        TransactionCategory category = TransactionCategory.INCOME;
        AssetTransactionType type = AssetTransactionType.Sell;

        DateTime date = this.ParseDate(record.Date);

        Transaction transaction = new Transaction(
            date,
            baseSymbol,
            new Money(amount < 0 ? -amount : amount, currency),
            category);

        return new AssetTransaction(
            transaction,
            baseSymbol,
            quantity,
            type);
    }

    private decimal CalculateCorporateActionQuantity(string? description, decimal amount, string currency)
    {
        try
        {
            decimal pricePerShare = this.ExtractPricePerShare(description ?? string.Empty);
            if (pricePerShare > 0)
            {
                decimal calculatedQuantity = amount / pricePerShare;
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

    private decimal ExtractPricePerShare(string description)
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
                if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
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
                if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
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
        decimal amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
        string currency = record.PriceCurrency ?? "EUR";

        if (currency == "-" || string.IsNullOrEmpty(currency))
        {
            currency = "EUR";
        }

        TransactionCategory category = TransactionCategory.EXPENSE;

        string transactionType = (record.TransactionType ?? string.Empty).ToLower();
        string descLower = (record.Description ?? string.Empty).ToLower();

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

        decimal quantity = this.ParseAmount(record.Quantity ?? "0");
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
            new Money(amount < 0 ? -amount : amount, currency),
            category);
    }

    private AssetTransaction MapToAssetTransaction(IBKRTransactionRecord record)
    {
        decimal quantity = this.ParseAmount(record.Quantity ?? "0");
        decimal amount = this.ParseAmount(record.NetAmount ?? record.GrossAmount ?? "0");
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
            new Money(amount < 0 ? -amount : amount, currency),
            category);

        decimal absQuantity = quantity < 0 ? -quantity : quantity;
        return new AssetTransaction(
            transaction,
            symbol,
            absQuantity > 0 ? absQuantity : 1,
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

    private DateTime ParseDate(string? dateStr)
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

    private decimal ParseAmount(string amountStr)
    {
        if (string.IsNullOrEmpty(amountStr))
        {
            return 0;
        }

        amountStr = amountStr.Trim().Replace(",", string.Empty);

        if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        return 0;
    }
}

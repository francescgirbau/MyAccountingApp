namespace MyAccountingApp.Core.Imports.Coinbase;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class CoinbaseImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<Transaction> transactions = new List<Transaction>(lines.Length);
        List<AssetTransaction> assetTransactions = new List<AssetTransaction>(lines.Length);

        foreach (string line in lines.Skip(2))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> fields = BankCsvImportService.ParseCsvLine(line);
            if (fields.Count < 10)
            {
                continue;
            }

            if (!TryParseTimestamp(fields[1], out DateTime date))
            {
                continue;
            }

            if (!decimal.TryParse(fields[4], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal quantity) || quantity == 0)
            {
                continue;
            }

            if (!TryParseMoney(fields[8], out decimal total))
            {
                continue;
            }

            string type = fields[2].Trim();
            string asset = fields[3].Trim();
            string currency = fields[5].Trim();
            string description = string.IsNullOrWhiteSpace(fields[10]) ? $"{type} {asset}" : fields[10].Trim();

            switch (type.ToUpperInvariant())
            {
                case "BUY":
                    assetTransactions.Add(CreateAssetTransaction(date, asset, currency, quantity, total, description, AssetTransactionType.Buy));
                    break;

                case "SELL":
                    assetTransactions.Add(CreateAssetTransaction(date, asset, currency, quantity, total, description, AssetTransactionType.Sell));
                    break;

                case "DEPOSIT":
                    transactions.Add(new Transaction(date, description, new Money(Math.Abs(total), currency), TransactionCategory.DEPOSIT));
                    break;

                case "WITHDRAWAL":
                    transactions.Add(new Transaction(date, description, new Money(Math.Abs(total), currency), TransactionCategory.TRANSFER));
                    break;
            }
        }

        return (transactions, assetTransactions, Enumerable.Empty<OptionTransaction>());
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static AssetTransaction CreateAssetTransaction(
        DateTime date,
        string symbol,
        string currency,
        decimal quantity,
        decimal total,
        string description,
        AssetTransactionType type)
    {
        TransactionCategory category = type == AssetTransactionType.Buy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;
        Transaction transaction = new Transaction(date, description, new Money(Math.Abs(total), currency), category);

        return new AssetTransaction(transaction, symbol, Math.Abs(quantity), type);
    }

    private static bool TryParseTimestamp(string value, out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseMoney(string value, out decimal result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string cleaned = value.Trim().Replace("€", string.Empty).Replace("$", string.Empty).Replace("£", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
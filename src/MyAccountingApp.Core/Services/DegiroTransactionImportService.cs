namespace MyAccountingApp.Core.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public partial class DegiroTransactionImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<AssetTransaction> assetTransactions = new();

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                List<string> fields = BankCsvImportService.ParseCsvLine(line);
                if (fields.Count < 17)
                {
                    continue;
                }

                DateTime date = ParseDate(fields[0]);
                string producto = fields[2];
                string isin = fields[3];
                int quantity = int.Parse(fields[6], NumberStyles.Any, CultureInfo.InvariantCulture);
                decimal amount = ParseEuropeanDecimal(fields[9]);
                string currency = NormalizeCurrency(fields[8]);

                if (quantity <= 0 || amount == 0)
                {
                    continue;
                }

                bool isBuy = amount < 0;
                string symbol = ExtractSymbol(producto, isin);
                AssetTransactionType type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
                TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

                Money money = new Money(Math.Abs(amount), currency);
                Transaction transaction = new Transaction(date, producto, money, category);
                AssetTransaction assetTx = new AssetTransaction(transaction, symbol, quantity, type);
                assetTransactions.Add(assetTx);
            }
            catch
            {
            }
        }

        return (Array.Empty<Transaction>(), assetTransactions);
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }

    private static decimal ParseEuropeanDecimal(string value)
    {
        string cleaned = value.Replace(".", string.Empty).Replace(",", ".");
        return decimal.Parse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    private static string NormalizeCurrency(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
        {
            return "EUR";
        }

        return value.ToUpperInvariant();
    }

    private static string ExtractSymbol(string producto, string isin)
    {
        if (string.IsNullOrWhiteSpace(producto))
        {
            return string.IsNullOrWhiteSpace(isin) ? "UNKNOWN" : isin;
        }

        string trimmed = producto.TrimStart();

        if (trimmed.StartsWith("ADR ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ADR/GDR ", StringComparison.OrdinalIgnoreCase))
        {
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx > 0)
            {
                                int afterPrefix = spaceIdx + 1;
                string rest = trimmed[afterPrefix..].TrimStart();
                if (rest.StartsWith("ON ", StringComparison.OrdinalIgnoreCase))
                {
                    rest = rest[3..].TrimStart();
                }

                if (rest.Length > 0)
                {
                    int nextSpace = rest.IndexOf(' ');
                    return nextSpace > 0 ? rest[..nextSpace].ToUpperInvariant() : rest.ToUpperInvariant();
                }
            }
        }

        int firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }
}

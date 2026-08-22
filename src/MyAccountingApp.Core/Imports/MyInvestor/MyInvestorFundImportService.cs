namespace MyAccountingApp.Core.Imports.MyInvestor;
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

public class MyInvestorFundImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
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
                List<string> fields = BankCsvImportService.ParseCsvLine(line, ';');
                if (fields.Count < 5)
                {
                    continue;
                }

                if (!string.Equals(fields[4], "Finalizada", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string isin = fields[1];
                if (string.IsNullOrWhiteSpace(isin))
                {
                    continue;
                }

                DateTime date = ParseDate(fields[0]);
                (decimal amount, string currency) = ParseAmount(fields[2]);
                decimal quantity = CsvParsing.ParseEuropeanDecimal(fields[3]);

                if (amount <= 0 || quantity <= 0)
                {
                    continue;
                }

                // MyInvestor CSV only contains buy transactions (suscripciones)
                AssetTransactionType type = AssetTransactionType.Buy;
                TransactionCategory category = TransactionCategory.INVESTMENT;

                Money money = new Money(amount, currency);
                Transaction transaction = new Transaction(date, isin, money, category);
                AssetTransaction assetTx = new AssetTransaction(transaction, isin, quantity, type);
                assetTransactions.Add(assetTx);
            }
            catch
            {
            }
        }

        return (Array.Empty<Transaction>(), assetTransactions, Enumerable.Empty<OptionTransaction>());
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }

    private static (decimal Amount, string Currency) ParseAmount(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string currency = parts.Length > 1 ? parts[^1].ToUpperInvariant() : "EUR";
        decimal amount = CsvParsing.ParseEuropeanDecimal(value);
        return (amount, currency);
    }
}

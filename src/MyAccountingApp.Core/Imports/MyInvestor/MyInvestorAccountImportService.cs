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

public class MyInvestorAccountImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<Transaction> transactions = new();

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

                if (string.IsNullOrWhiteSpace(fields[2]))
                {
                    continue;
                }

                DateTime date = ParseDate(fields[0]);
                string concepto = fields[2];
                decimal amount = CsvParsing.ParseEuropeanDecimal(fields[3]);
                string currency = fields[4];

                if (amount == 0)
                {
                    continue;
                }

                TransactionCategory category = amount >= 0
                    ? TransactionCategory.INCOME
                    : TransactionCategory.EXPENSE;

                category = BankCsvImportService.DetectTransfer(concepto, category);

                Money money = new Money(Math.Abs(amount), currency);
                Transaction transaction = new Transaction(date, concepto, money, category);
                transactions.Add(transaction);
            }
            catch
            {
            }
        }

        return (transactions, Array.Empty<AssetTransaction>(), Enumerable.Empty<OptionTransaction>());
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
}

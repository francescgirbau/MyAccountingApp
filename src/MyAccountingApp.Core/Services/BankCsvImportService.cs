namespace MyAccountingApp.Core.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class BankCsvImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<Transaction> transactions = new List<Transaction>(lines.Length);

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                List<string> fields = ParseCsvLine(line);
                if (fields.Count < 6)
                {
                    continue;
                }

                DateTime date = DateTime.Parse(fields[0], CultureInfo.InvariantCulture);
                string description = fields[1];
                decimal amount = decimal.Parse(fields[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                string currency = fields[3];
                string categoryStr = fields[5];

                TransactionCategory category = Enum.TryParse<TransactionCategory>(categoryStr, ignoreCase: true, out var parsed)
                    ? parsed
                    : amount >= 0
                        ? TransactionCategory.INCOME
                        : TransactionCategory.EXPENSE;

                Money money = new Money(Math.Abs(amount), currency);
                Transaction transaction = new Transaction(date, description, money, category);
                transactions.Add(transaction);
            }
            catch
            {
            }
        }

        return (transactions, Array.Empty<AssetTransaction>());
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    internal static List<string> ParseCsvLine(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        StringBuilder current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }
}

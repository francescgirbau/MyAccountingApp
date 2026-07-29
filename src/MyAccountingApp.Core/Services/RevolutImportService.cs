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

public class RevolutImportService : IBrokerImportService
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
                List<string> fields = BankCsvImportService.ParseCsvLine(line);
                if (fields.Count < 10)
                {
                    continue;
                }

                string type = fields[0].Trim();
                string description = fields[4].Trim();
                string amountStr = fields[5].Trim();
                string feeStr = fields[6].Trim();
                string currency = fields[7].Trim();
                string state = fields[8].Trim();

                if (state != "COMPLETED")
                {
                    continue;
                }

                decimal amount = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                decimal fee = decimal.Parse(feeStr, NumberStyles.Any, CultureInfo.InvariantCulture);

                if (amount == 0 && fee == 0)
                {
                    continue;
                }

                DateTime date = ParseDate(fields[2]);

                TransactionCategory? category = Classify(type, description, amount);
                if (category is null)
                {
                    continue;
                }

                if (amount != 0)
                {
                    Money money = new(Math.Abs(amount), currency);
                    transactions.Add(new Transaction(date, description, money, category.Value));
                }

                if (fee > 0)
                {
                    Money feeMoney = new(fee, currency);
                    transactions.Add(new Transaction(date, $"{description} (fee)", feeMoney, TransactionCategory.EXPENSE));
                }
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
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }

    private static TransactionCategory? Classify(string type, string description, decimal amount)
    {
        string upper = type.ToUpperInvariant();

        if (upper == "DEPOSIT")
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper == "CARD PAYMENT")
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper == "CARD REFUND")
        {
            return TransactionCategory.INCOME;
        }

        if (upper == "ATM")
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper == "TRANSFER")
        {
            string descUpper = description.ToUpperInvariant();

            if (descUpper.Contains("BALANCE MIGRATION"))
            {
                return null;
            }

            if (descUpper.Contains("FROM CUENTA FLEXIBLE"))
            {
                return TransactionCategory.INCOME;
            }

            if (amount > 0)
            {
                return TransactionCategory.INCOME;
            }

            return TransactionCategory.EXPENSE;
        }

        return null;
    }
}

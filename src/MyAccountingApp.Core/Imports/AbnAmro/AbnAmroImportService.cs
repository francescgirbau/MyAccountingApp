namespace MyAccountingApp.Core.Imports.AbnAmro;
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

public class AbnAmroImportService : IBrokerImportService
{
    private static readonly string[] TransferKeywords = { "J.P.MORGAN", "DEGIRO", "INTERACTIVE BROKERS", "REVOLUT" };
    private static readonly string[] IncomeKeywords = { "SPECTRAL", "TESLA INTERNATIONAL" };
    private static readonly string[] ExpenseKeywords = { "B GALENDE", "BERTA GALENDE" };

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
                if (fields.Count < 8)
                {
                    continue;
                }

                string mutationCode = fields[1].Trim();
                string description = fields[7].Trim();
                string dateStr = fields[2].Trim();
                string amountStr = fields[6].Trim();

                if (string.IsNullOrEmpty(amountStr))
                {
                    continue;
                }

                decimal amount = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);

                if (amount == 0)
                {
                    continue;
                }

                DateTime date = DateTime.ParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture);

                TransactionCategory category = Classify(mutationCode, description, amount);

                Money money = new(Math.Abs(amount), "EUR");
                transactions.Add(new Transaction(date, description, money, category));
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

    private static TransactionCategory Classify(string mutationCode, string description, decimal amount)
    {
        string descUpper = description.ToUpperInvariant();
        string codeUpper = mutationCode.ToUpperInvariant();

        if (descUpper.Contains("ABN AMRO BANK N.V.") || descUpper.Contains("ABN AMRO BANK"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (codeUpper.Contains("BETAALPAS") || codeUpper == "BEA")
        {
            return TransactionCategory.EXPENSE;
        }

        if (codeUpper.Contains("IDEAL"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (codeUpper.Contains("INCASSO"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (codeUpper == "GEA")
        {
            return TransactionCategory.EXPENSE;
        }

        if (codeUpper.Contains("OVERBOEKING") || codeUpper.Contains("PERIODIEKE"))
        {
            return ClassifyByDescription(descUpper, amount);
        }

        if (codeUpper == "EUR" || codeUpper == "DIVERSEN" || string.IsNullOrEmpty(codeUpper))
        {
            return ClassifyByDescription(descUpper, amount);
        }

        if (amount < 0)
        {
            return TransactionCategory.EXPENSE;
        }

        return TransactionCategory.INCOME;
    }

    private static TransactionCategory ClassifyByDescription(string descUpper, decimal amount)
    {
        if (TransferKeywords.Any(k => descUpper.Contains(k)))
        {
            return TransactionCategory.TRANSFER;
        }

        if (IncomeKeywords.Any(k => descUpper.Contains(k)))
        {
            return TransactionCategory.INCOME;
        }

        if (ExpenseKeywords.Any(k => descUpper.Contains(k)))
        {
            return TransactionCategory.EXPENSE;
        }

        if (descUpper.Contains("TIKKIE"))
        {
            return TransactionCategory.INCOME;
        }

        if (descUpper.Contains("SEPA OVERBOEKING"))
        {
            return TransactionCategory.TRANSFER;
        }

        if (descUpper.Contains("FRANCESC GIRBAU LLISTUELLA") || descUpper.Contains("F GIRBAU"))
        {
            return TransactionCategory.TRANSFER;
        }

        if (amount < 0)
        {
            return TransactionCategory.EXPENSE;
        }

        return TransactionCategory.INCOME;
    }
}

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

                (TransactionCategory category, bool needsReview) = Classify(mutationCode, description, amount);

                Money money = new(Math.Abs(amount), "EUR");
                Transaction transaction = new(date, description, money, category);
                if (needsReview)
                {
                    transaction.MarkNeedsReview();
                }

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

    private static (TransactionCategory Category, bool NeedsReview) Classify(string mutationCode, string description, decimal amount)
    {
        string descUpper = description.ToUpperInvariant();
        string codeUpper = mutationCode.ToUpperInvariant();

        // Regles de codi de mutació inqüestionables: sempre despesa, mai necessiten revisió.
        if (descUpper.Contains("ABN AMRO BANK N.V.") || descUpper.Contains("ABN AMRO BANK"))
        {
            return (TransactionCategory.EXPENSE, false);
        }

        if (codeUpper.Contains("BETAALPAS") || codeUpper == "BEA")
        {
            return (TransactionCategory.EXPENSE, false);
        }

        if (codeUpper.Contains("IDEAL"))
        {
            return (TransactionCategory.EXPENSE, false);
        }

        if (codeUpper.Contains("INCASSO"))
        {
            return (TransactionCategory.EXPENSE, false);
        }

        if (codeUpper == "GEA")
        {
            return (TransactionCategory.EXPENSE, false);
        }

        // Per a qualsevol altre codi de mutació, la descripció mana sobre el signe:
        // les keywords es valoren primer, i el signe només és l'últim recurs.
        return ClassifyByDescription(descUpper, amount);
    }

    private static (TransactionCategory Category, bool NeedsReview) ClassifyByDescription(string descUpper, decimal amount)
    {
        if (TransferKeywords.Any(k => descUpper.Contains(k)))
        {
            return (TransactionCategory.TRANSFER, false);
        }

        if (IncomeKeywords.Any(k => descUpper.Contains(k)))
        {
            return (TransactionCategory.INCOME, false);
        }

        if (ExpenseKeywords.Any(k => descUpper.Contains(k)))
        {
            return (TransactionCategory.EXPENSE, false);
        }

        if (descUpper.Contains("TIKKIE"))
        {
            return (TransactionCategory.INCOME, false);
        }

        if (descUpper.Contains("SEPA OVERBOEKING"))
        {
            return (TransactionCategory.TRANSFER, false);
        }

        if (descUpper.Contains("FRANCESC GIRBAU LLISTUELLA") || descUpper.Contains("F GIRBAU"))
        {
            return (TransactionCategory.TRANSFER, false);
        }

        if (descUpper.Contains("INCASSO"))
        {
            return (TransactionCategory.EXPENSE, false);
        }

        // Cap keyword no ha confirmat la categoria: el signe és l'últim recurs,
        // però és una classificació sospitosa que necessita supervisió manual.
        if (amount < 0)
        {
            return (TransactionCategory.EXPENSE, true);
        }

        return (TransactionCategory.INCOME, true);
    }
}

namespace MyAccountingApp.Core.Imports.Degiro;
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

public class DegiroImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<Transaction> transactions = new();
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
                if (fields.Count < 12)
                {
                    continue;
                }

                DateTime date = ParseDate(fields[0]);
                string description = fields[5];
                string currency = NormalizeCurrency(fields[7], fields[9]);
                decimal amount = CsvParsing.ParseEuropeanDecimal(fields[8]);

                if (string.IsNullOrWhiteSpace(description) || amount == 0)
                {
                    continue;
                }

                if (IsBuySell(description))
                {
                    continue;
                }

                Transaction? tx = CreateCashTransaction(date, description, amount, currency);
                if (tx is not null)
                {
                    transactions.Add(tx);
                }
            }
            catch
            {
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

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        if (DateTime.TryParse(value, CultureInfo.CreateSpecificCulture("ca-ES"), DateTimeStyles.None, out date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string NormalizeCurrency(string variationCurrency, string balanceCurrency)
    {
        string c = variationCurrency;
        if (string.IsNullOrWhiteSpace(c) || c.Length != 3 || c == "---")
        {
            c = balanceCurrency;
        }

        if (string.IsNullOrWhiteSpace(c) || c.Length != 3)
        {
            c = "EUR";
        }

        return c.ToUpperInvariant();
    }

    private static bool IsBuySell(string description)
    {
        return description.StartsWith("Compra ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Venta ", StringComparison.OrdinalIgnoreCase);
    }

    private static Transaction? CreateCashTransaction(
        DateTime date, string description, decimal amount, string currency)
    {
        TransactionCategory? category = ClassifyDescription(description, amount);
        if (category is null)
        {
            return null;
        }

        Money money = new Money(Math.Abs(amount), currency);
        return new Transaction(date, description, money, category.Value);
    }

    private static TransactionCategory? ClassifyDescription(string description, decimal amount)
    {
        string upper = description.ToUpperInvariant();

        if (upper.Contains("CASH SWEEP") || upper.Contains("CASH SWEEP TRANSFER"))
        {
            return null;
        }

        if (upper.Contains("TRANSFERIR") && upper.Contains("CUENTA DE EFECTIVO"))
        {
            return null;
        }

        if (upper.Contains("CAMBIO DE DIVISA") || upper == "FX")
        {
            return null;
        }

        if (upper.Contains("OPCIÓN"))
        {
            return null;
        }

        if (upper.Contains("COSTE DE LA ACCIÓN"))
        {
            return null;
        }

        if (upper == "INGRESO")
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper.Contains("DIVIDENDO") || upper.Contains("DIVIDEND"))
        {
            return amount >= 0 ? TransactionCategory.DIVIDEND : TransactionCategory.EXPENSE;
        }

        if (upper.Contains("RETENCI") || upper.Contains("WITHHOLDING"))
        {
            return TransactionCategory.WITHHOLDING_TAX;
        }

        if (upper.Contains("COSTES") || upper.Contains("COST") || upper.Contains("COMISI"))
        {
            return TransactionCategory.FEE;
        }

        if (upper.Contains("TAX") || upper.Contains("IMPUESTO"))
        {
            return TransactionCategory.WITHHOLDING_TAX;
        }

        if (upper.Contains("INTEREST") || upper.Contains("INTERES"))
        {
            return amount >= 0 ? TransactionCategory.INTEREST : TransactionCategory.EXPENSE;
        }

        if (upper.Contains("PRESTAMO") || upper.Contains("PRÉSTAMO") || upper.Contains("LENDING"))
        {
            return TransactionCategory.INCOME;
        }

        if (upper.Contains("PASS-THROUGH") || upper.Contains("PASS THROUGH") || upper.Contains("PASSTHROUGH"))
        {
            return TransactionCategory.FEE;
        }

        if (upper.Contains("FLATEX") || upper.Contains("DEPOSIT"))
        {
            if (upper.Contains("WITHDRAWAL"))
            {
                return TransactionCategory.TRANSFER;
            }

            if (upper.Contains("PROCESSED"))
            {
                return null;
            }

            return TransactionCategory.DEPOSIT;
        }

        return amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
    }
}

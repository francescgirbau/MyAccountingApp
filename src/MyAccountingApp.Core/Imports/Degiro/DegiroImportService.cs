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
        List<FxRow> fxRows = new();

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

                if (IsFxDescription(description))
                {
                    fxRows.Add(new FxRow(
                        Date: date,
                        Time: fields[1],
                        ValueDate: TryParseValueDate(fields[2]),
                        Product: fields[3],
                        Rate: ParseNullableRate(fields[6]),
                        Currency: currency,
                        Amount: amount,
                        OrderId: fields[11],
                        IsIn: description.StartsWith("Ingreso", StringComparison.OrdinalIgnoreCase)));
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

        transactions.AddRange(BuildFxConversions(fxRows));

        return (transactions, assetTransactions, Enumerable.Empty<OptionTransaction>());
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static List<Transaction> BuildFxConversions(List<FxRow> rows)
    {
        List<Transaction> result = new();
        List<FxRow> remaining = rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Time, StringComparer.Ordinal)
            .ThenBy(r => r.OrderId, StringComparer.Ordinal)
            .ToList();

        while (remaining.Count > 0)
        {
            FxRow first = remaining[0];
            remaining.RemoveAt(0);
            FxRow? match = remaining.FirstOrDefault(candidate => TryMatchFxRows(first, candidate));

            if (match is null)
            {
                result.Add(CreateSingleFxLeg(first));
                continue;
            }

            remaining.Remove(match);
            Guid pairId = Guid.NewGuid();
            result.Add(CreatePairLeg(pairId, first, match));
            result.Add(CreatePairLeg(pairId, match, first));
        }

        return result;
    }

    private static bool TryMatchFxRows(FxRow a, FxRow b)
    {
        if (a.IsIn == b.IsIn)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(a.OrderId) && a.OrderId == b.OrderId)
        {
            return true;
        }

        if (a.Date == b.Date && string.Equals(a.Time, b.Time, StringComparison.Ordinal) && a.Currency != b.Currency)
        {
            return true;
        }

        if ((a.ValueDate is null || b.ValueDate is null) ||
            Math.Abs((a.ValueDate.Value - b.ValueDate.Value).TotalDays) > 1)
        {
            return false;
        }

        decimal? rate = a.Rate ?? b.Rate;
        if (rate is null)
        {
            return false;
        }

        decimal inAmount = Math.Abs(a.Amount);
        decimal outAmount = Math.Abs(b.Amount);
        return MatchesRate(inAmount / outAmount, rate.Value) || MatchesRate(outAmount / inAmount, rate.Value);
    }

    private static bool MatchesRate(decimal implied, decimal rate)
    {
        return Math.Abs(implied - rate) / rate <= RateTolerance;
    }

    private static Transaction CreateSingleFxLeg(FxRow row)
    {
        Transaction tx = new(
            row.Date,
            BuildFxDescription(row.Currency, null, row.Product),
            new Money(Math.Abs(row.Amount), row.Currency),
            TransactionCategory.FX_CONVERSION);
        tx.SetFxPair(
            Guid.NewGuid(),
            row.IsIn ? FxLeg.In : FxLeg.Out,
            row.Rate,
            string.IsNullOrWhiteSpace(row.OrderId) ? null : row.OrderId);
        return tx;
    }

    private static Transaction CreatePairLeg(Guid pairId, FxRow self, FxRow other)
    {
        string outCurrency = self.IsIn ? other.Currency : self.Currency;
        string inCurrency = self.IsIn ? self.Currency : other.Currency;
        Transaction tx = new(
            self.Date,
            BuildFxDescription(outCurrency, inCurrency, self.Product),
            new Money(Math.Abs(self.Amount), self.Currency),
            TransactionCategory.FX_CONVERSION);
        tx.SetFxPair(
            pairId,
            self.IsIn ? FxLeg.In : FxLeg.Out,
            self.Rate ?? other.Rate,
            string.IsNullOrWhiteSpace(self.OrderId) ? null : self.OrderId);
        return tx;
    }

    private static string BuildFxDescription(string outCurrency, string? inCurrency, string? product)
    {
        string exchange = inCurrency is null
            ? $"FX {outCurrency}"
            : $"FX {outCurrency}→{inCurrency}";
        return string.IsNullOrWhiteSpace(product) ? exchange : $"{exchange} · {product}";
    }

    private const decimal RateTolerance = 0.005m;

    private sealed record FxRow(
        DateTime Date,
        string Time,
        DateTime? ValueDate,
        string? Product,
        decimal? Rate,
        string Currency,
        decimal Amount,
        string? OrderId,
        bool IsIn);

    private static DateTime? TryParseValueDate(string value)
    {
        try
        {
            return ParseDate(value);
        }
        catch
        {
            return null;
        }
    }

    private static decimal? ParseNullableRate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        decimal rate = CsvParsing.ParseEuropeanDecimal(value);
        return rate == 0 ? null : rate;
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

    private static bool IsFxDescription(string description)
    {
        return description.Contains("CAMBIO DE DIVISA", StringComparison.OrdinalIgnoreCase);
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

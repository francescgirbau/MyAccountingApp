namespace MyAccountingApp.Core.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public partial class DegiroImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
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
                string producto = fields[3];
                string description = fields[5];
                string isin = fields[4];
                string currency = NormalizeCurrency(fields[7], fields[9]);
                decimal amount = ParseEuropeanDecimal(fields[8]);

                if (string.IsNullOrWhiteSpace(description) || amount == 0)
                {
                    continue;
                }

                if (IsBuySell(description))
                {
                    var (assetTx, cashTx) = CreateAssetTransaction(date, description, isin, producto, amount, currency);
                    assetTransactions.Add(assetTx);
                    if (cashTx is not null)
                    {
                        transactions.Add(cashTx);
                    }
                }
                else
                {
                    Transaction? tx = CreateCashTransaction(date, description, amount, currency);
                    if (tx is not null)
                    {
                        transactions.Add(tx);
                    }
                }
            }
            catch
            {
            }
        }

        return (transactions, assetTransactions);
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

    private static decimal ParseEuropeanDecimal(string value)
    {
        string cleaned = value.Replace(".", string.Empty).Replace(",", ".");
        return decimal.Parse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture);
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

    private static int ExtractQuantity(string description)
    {
        Match m = QuantityPattern().Match(description);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int qty))
        {
            return qty;
        }

        return 1;
    }

    [GeneratedRegex(@"^(?:Compra|Venta)\s+(\d+)\s", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex QuantityPattern();

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
                string rest = trimmed[(spaceIdx + 1)..].TrimStart();
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

    private static (AssetTransaction, Transaction?) CreateAssetTransaction(
        DateTime date, string description, string isin, string producto, decimal amount, string currency)
    {
        bool isBuy = description.StartsWith("Compra ", StringComparison.OrdinalIgnoreCase);
        int quantity = ExtractQuantity(description);
        AssetTransactionType type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

        string symbol = ExtractSymbol(producto, isin);
        Money money = new Money(Math.Abs(amount), currency);
        Transaction transaction = new Transaction(date, description, money, category);
        AssetTransaction assetTx = new AssetTransaction(transaction, symbol, quantity, type);
        return (assetTx, null);
    }

    private static Transaction? CreateCashTransaction(
        DateTime date, string description, decimal amount, string currency)
    {
        TransactionCategory category = ClassifyDescription(description, amount);
        if (category == TransactionCategory.DEPOSIT)
        {
            return null;
        }

        Money money = new Money(Math.Abs(amount), currency);
        return new Transaction(date, description, money, category);
    }

    private static TransactionCategory ClassifyDescription(string description, decimal amount)
    {
        string upper = description.ToUpperInvariant();

        if (upper.Contains("DIVIDENDO") || upper.Contains("DIVIDEND"))
        {
            return amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
        }

        if (upper.Contains("RETENCI") || upper.Contains("WITHHOLDING"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper.Contains("COSTES") || upper.Contains("COST") || upper.Contains("COMISI"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper.Contains("TAX") || upper.Contains("IMPUESTO"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper.Contains("INTEREST") || upper.Contains("INTERES"))
        {
            return amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
        }

        if (upper.Contains("PRESTAMO") || upper.Contains("LENDING"))
        {
            return TransactionCategory.INCOME;
        }

        if (upper.Contains("PASS-THROUGH") || upper.Contains("PASS THROUGH") || upper.Contains("PASSTHROUGH"))
        {
            return TransactionCategory.EXPENSE;
        }

        if (upper.Contains("CASH SWEEP") || upper.Contains("TRANSFERIR") || upper.Contains("TRANSFER"))
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper.Contains("DEPOSIT") || upper.Contains("WITHDRAWAL"))
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper.Contains("FLATEX"))
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper.Contains("CAMBIO DE DIVISA") || upper.Contains("FX") || upper.Contains("EXCHANGE"))
        {
            return TransactionCategory.DEPOSIT;
        }

        if (upper.Contains("PROCESSED"))
        {
            return TransactionCategory.DEPOSIT;
        }

        return amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
    }
}

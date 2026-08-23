namespace MyAccountingApp.Core.Imports.SelfBank;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class SelfBankFundImportService : IBrokerImportService
{
    private const string HeaderPrefix = "Fecha movimiento;Fecha valor;Movimiento;Valor";

    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, Encoding.Latin1, cancellationToken);
        List<AssetTransaction> assetTransactions = new();

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                List<string> fields = BankCsvImportService.ParseCsvLine(line, ';');
                if (fields.Count < 11)
                {
                    continue;
                }

                AssetTransactionType? type = MapType(fields[2]);
                if (type is null)
                {
                    continue;
                }

                string fundName = fields[3];
                if (string.IsNullOrWhiteSpace(fundName))
                {
                    continue;
                }

                DateTime date = ParseDate(fields[0]);
                decimal quantity = CsvParsing.ParseEuropeanDecimal(fields[4]);
                decimal amount = Math.Abs(CsvParsing.ParseEuropeanDecimal(fields[6]));

                if (quantity <= 0 || amount <= 0)
                {
                    continue;
                }

                string symbol = BuildSymbol(fundName);
                TransactionCategory category = type == AssetTransactionType.Buy ? TransactionCategory.INVESTMENT : TransactionCategory.DIVESTMENT;

                Money money = new Money(amount, "EUR");
                Transaction transaction = new Transaction(date, fundName, money, category);
                AssetTransaction assetTx = new AssetTransaction(transaction, symbol, quantity, type.Value);
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

    private static AssetTransactionType? MapType(string movimiento)
    {
        string m = movimiento.ToUpperInvariant();

        if (m.Contains("SUSCRIPCI") || m.Contains("TRASPASO DE ENTRADA"))
        {
            return AssetTransactionType.Buy;
        }

        if (m.Contains("REEMBOLSO") || m.Contains("TRASPASO DE SALIDA"))
        {
            return AssetTransactionType.Sell;
        }

        return null;
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string BuildSymbol(string fundName)
    {
        string normalized = RemoveDiacritics(fundName).ToUpperInvariant().Trim();
        normalized = normalized.Replace(",", string.Empty).Replace(" FI", string.Empty).Trim();
        return normalized.Replace(" ", "_");
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new();
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

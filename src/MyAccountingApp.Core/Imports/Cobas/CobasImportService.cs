namespace MyAccountingApp.Core.Imports.Cobas;
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

public class CobasImportService : IBrokerImportService
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
                List<string> fields = BankCsvImportService.ParseCsvLine(line);
                if (fields.Count < 8)
                {
                    continue;
                }

                if (!string.Equals(fields[4], "Finalizada", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AssetTransactionType? type = MapType(fields[3]);
                if (type is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fields[^1]))
                {
                    continue;
                }

                DateTime date = ParseDate(fields[2]);
                decimal amount = CsvParsing.ParseEuropeanDecimal(JoinAmountFields(fields));
                decimal quantity = CsvParsing.ParseEuropeanDecimal(fields[^1]);

                if (amount <= 0 || quantity <= 0)
                {
                    continue;
                }

                string producto = fields[1];
                string symbol = BuildSymbol(producto);
                TransactionCategory category = type == AssetTransactionType.Buy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;

                Money money = new Money(amount, "EUR");
                Transaction transaction = new Transaction(date, producto, money, category);
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

    private static AssetTransactionType? MapType(string tipo)
    {
        if (tipo.Contains("Suscripción", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("Traspaso de entrada", StringComparison.OrdinalIgnoreCase))
        {
            return AssetTransactionType.Buy;
        }

        if (tipo.Contains("Reembolso", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("Traspaso de salida", StringComparison.OrdinalIgnoreCase))
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

    private static string JoinAmountFields(IReadOnlyList<string> fields)
    {
        // Importe may be split by an unquoted comma (e.g. "2.560,32 € (Bruto)");
        // Valor liquidativo and Participaciones are always the last two fields.
return string.Join(",", fields.Skip(5).Take(fields.Count - 7));
    }

    private static string BuildSymbol(string producto)
    {
        string normalized = RemoveDiacritics(producto).ToUpperInvariant().Trim();
        string clasePart = string.Empty;
        int claseIdx = normalized.IndexOf(" CLASE ", StringComparison.Ordinal);
        if (claseIdx >= 0)
        {
            clasePart = "_" + normalized[(claseIdx + 7) ..].Trim();
            normalized = normalized[..claseIdx].Trim();
        }

        normalized = normalized.Replace(",", string.Empty).Replace(" FI", string.Empty).Trim();
        return normalized.Replace(" ", "_") + clasePart;
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

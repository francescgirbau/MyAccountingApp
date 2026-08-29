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

public class SelfBankAccountImportService : IBrokerImportService
{
    private static readonly string[] TransferOutKeywords = { "TRF A COBAS", "TRF A FRANCESC", "TRF A GIRBAU" };
    private static readonly string[] TransferInKeywords = { "TFI RECIBIDA", "ABONO TRF DE F GIRBAU", "ABONO TRF DE FRANCESC", "ABONO TRF DE GIRBAU" };
    private static readonly string[] TermDepositKeywords = { "APERTURA DEPOSITO", "VENCIMIENTO DEPOSITO" };
    private static readonly string[] InterestKeywords = { "INTERESES DEPOSITO", "INTERESES " };
    private static readonly string[] ExpenseKeywords = { "CLUB TRIATLO", "O2 MOVIL", "MUTUALITAT", "O2 MÓVIL" };

    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, Encoding.Latin1, cancellationToken);
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
                string movimiento = fields[2];
                decimal amount = decimal.Parse(fields[4], NumberStyles.Any, CultureInfo.InvariantCulture);

                if (amount == 0)
                {
                    continue;
                }

                TransactionCategory category = ClassifySelfBank(movimiento, amount);

                Money money = new Money(Math.Abs(amount), "EUR");
                Transaction transaction = new Transaction(date, movimiento, money, category);
                transactions.Add(transaction);
            }
            catch
            {
            }
        }

        return (transactions, Array.Empty<AssetTransaction>(), Enumerable.Empty<OptionTransaction>());
    }

    private static TransactionCategory ClassifySelfBank(string movimiento, decimal amount)
    {
        string m = movimiento.ToUpperInvariant();

        // Outgoing transfers to broker (Cobas, Francesc, etc.)
        if (TransferOutKeywords.Any(k => m.Contains(k)))
        {
            return TransactionCategory.TRANSFER;
        }

        // Incoming transfers from broker
        if (TransferInKeywords.Any(k => m.Contains(k)))
        {
            return TransactionCategory.TRANSFER;
        }

        // Term deposits (internal moves)
        if (TermDepositKeywords.Any(k => m.Contains(k)))
        {
            return TransactionCategory.TRANSFER;
        }

        // Interest income
        if (InterestKeywords.Any(k => m.Contains(k)))
        {
            return TransactionCategory.INTEREST;
        }

        // Known expenses
        if (ExpenseKeywords.Any(k => m.Contains(k)))
        {
            return TransactionCategory.EXPENSE;
        }

        // Fallback: amount sign
        return amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture);
    }
}

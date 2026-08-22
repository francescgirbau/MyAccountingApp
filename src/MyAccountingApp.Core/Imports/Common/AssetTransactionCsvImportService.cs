namespace MyAccountingApp.Core.Imports.Common;
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

public class AssetTransactionCsvImportService : IBrokerImportService
{
    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<AssetTransaction> assetTransactions = new List<AssetTransaction>(lines.Length);

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                List<string> fields = BankCsvImportService.ParseCsvLine(line);
                if (fields.Count < 6)
                {
                    continue;
                }

                DateTime date;
                if (!DateTime.TryParse(fields[0], CultureInfo.CreateSpecificCulture("ca-ES"), DateTimeStyles.None, out date))
                {
                    date = DateTime.Parse(fields[0], CultureInfo.InvariantCulture);
                }

                string description = fields[1];
                string ticker = fields[2];
                decimal import = decimal.Parse(fields[3], NumberStyles.Any, CultureInfo.InvariantCulture);
                string currency = fields[4];

                bool isBuy = import < 0;
                AssetTransactionType type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
                TransactionCategory category = isBuy ? TransactionCategory.INVESTMENT : TransactionCategory.DIVESTMENT;
                category = BankCsvImportService.DetectTransfer(description, category);

                Money money = new Money(Math.Abs(import), currency);
                Transaction transaction = new Transaction(date, description, money, category);
                AssetTransaction assetTx = new AssetTransaction(transaction, ticker, 1, type);
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
}

namespace MyAccountingApp.Core.Agents.IBKR;

using System;
using System.Collections.Generic;
using System.Globalization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class CorporateActionAgent : IIBKRStatementAgent
{
    public string SectionName => "Corporate Actions";

    public void Parse(IReadOnlyList<string[]> rows, List<Transaction> transactions, List<AssetTransaction> assetTransactions, List<OptionTransaction> optionTransactions, List<string> errors)
    {
        foreach (string[] fields in rows)
        {
            if (fields.Length < 10) continue;
            if (fields[1] != "Data") continue;

            string assetCategory = fields[2];
            string currency = fields[3];
            string dateStr = fields[5];
            string description = fields[6];
            string qtyStr = fields[7];
            string proceedsStr = fields[8];

            if (!TryParseDateTime(dateStr, out DateTime date)) continue;
            if (!TryParseDecimal(qtyStr, out decimal rawQuantity) || rawQuantity == 0) continue;
            if (!TryParseDecimal(proceedsStr, out decimal proceeds)) continue;

            int quantity = (int)Math.Abs(rawQuantity);
            bool hasCash = Math.Abs(proceeds) > 0.01m;

            // Corporate actions with cash proceed = Sell event
            if (hasCash && proceeds > 0)
            {
                string symbol = ExtractSymbol(description);
                Money money = new Money(Math.Abs(proceeds), currency);
                Transaction transaction = new Transaction(date, description, money, TransactionCategory.INCOME);
                AssetTransaction assetTx = new AssetTransaction(transaction, symbol, quantity, AssetTransactionType.Sell);
                assetTransactions.Add(assetTx);
            }
        }
    }

    private static string ExtractSymbol(string description)
    {
        int parenIdx = description.IndexOf('(');
        if (parenIdx > 0)
        {
            return description[..parenIdx].Trim();
        }

        return "UNKNOWN";
    }

    private static bool TryParseDateTime(string value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (DateTime.TryParseExact(value, "yyyy-MM-dd, HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        return false;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string cleaned = value.Replace(",", string.Empty).Replace("\"", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}

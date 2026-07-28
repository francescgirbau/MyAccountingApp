namespace MyAccountingApp.Core.Agents.IBKR;

using System;
using System.Collections.Generic;
using System.Globalization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class DividendAgent : IIBKRStatementAgent
{
    public string SectionName => "Dividends";

    public void Parse(IReadOnlyList<string[]> rows, List<Transaction> transactions, List<AssetTransaction> assetTransactions, List<OptionTransaction> optionTransactions, List<string> errors)
    {
        foreach (string[] fields in rows)
        {
            if (fields.Length < 5) continue;
            if (fields[1] != "Data") continue;

            string currency = fields[2];
            string dateStr = fields[3];
            string description = fields[4];
            string amountStr = fields[5];

            if (!TryParseDate(dateStr, out DateTime date)) continue;
            if (!TryParseDecimal(amountStr, out decimal amount) || amount == 0) continue;

            Money money = new Money(Math.Abs(amount), currency);
            Transaction transaction = new Transaction(date, description, money, TransactionCategory.INCOME);
            transactions.Add(transaction);
        }
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string cleaned = value.Replace(",", string.Empty).Replace("\"", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}

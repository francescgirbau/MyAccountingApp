namespace MyAccountingApp.Core.Imports.IBKR;
using System;
using System.Collections.Generic;
using System.Globalization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class TradeAgent : IIBKRStatementAgent
{
    public string SectionName => "Trades";

    public void Parse(IReadOnlyList<string[]> rows, List<Transaction> transactions, List<AssetTransaction> assetTransactions, List<OptionTransaction> optionTransactions, List<string> errors)
    {
        foreach (string[] fields in rows)
        {
            if (fields.Length < 15)
            {
                continue;
            }

            if (fields[1] != "Data" || fields[2] != "Order")
            {
                continue;
            }

            string assetCategory = fields[3];
            string currency = fields[4];
            string symbol = fields[5];
            string dateTimeStr = fields[6];
            string qtyStr = fields[7];
            string proceedsStr = fields[10];

            if (!TryParseDateTime(dateTimeStr, out DateTime date))
            {
                continue;
            }

            if (!TryParseDecimal(qtyStr, out decimal rawQuantity) || rawQuantity == 0)
            {
                continue;
            }

            if (!TryParseDecimal(proceedsStr, out decimal proceeds) || proceeds == 0)
            {
                continue;
            }

            bool isBuy = proceeds < 0;
            int quantity = (int)Math.Abs(rawQuantity);

            if (assetCategory == "Equity and Index Options")
            {
                OptionTransaction optionTx = CreateOptionTransaction(date, symbol, currency, quantity, proceeds, isBuy);
                optionTransactions.Add(optionTx);
            }
            else if (assetCategory == "Stocks")
            {
                AssetTransaction assetTx = CreateAssetTransaction(date, symbol, currency, quantity, proceeds, isBuy);
                assetTransactions.Add(assetTx);
            }
        }
    }

    private static OptionTransaction CreateOptionTransaction(DateTime date, string symbol, string currency, int quantity, decimal proceeds, bool isBuy)
    {
        string description = symbol;
        string isin = string.Empty;
        string optionSymbol = ExtractUnderlyingSymbol(symbol);
        AssetTransactionType type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;
        Money premium = new Money(Math.Abs(proceeds), currency);
        Transaction transaction = new Transaction(date, description, premium, category);

        return new OptionTransaction(transaction, optionSymbol, isin, quantity, type);
    }

    private static AssetTransaction CreateAssetTransaction(DateTime date, string symbol, string currency, int quantity, decimal proceeds, bool isBuy)
    {
        string description = symbol;
        AssetTransactionType type = isBuy ? AssetTransactionType.Buy : AssetTransactionType.Sell;
        TransactionCategory category = isBuy ? TransactionCategory.EXPENSE : TransactionCategory.INCOME;
        Money money = new Money(Math.Abs(proceeds), currency);
        Transaction transaction = new Transaction(date, description, money, category);

        return new AssetTransaction(transaction, symbol, quantity, type);
    }

    private static string ExtractUnderlyingSymbol(string symbol)
    {
        int spaceIdx = symbol.IndexOf(' ');
        return spaceIdx > 0 ? symbol[..spaceIdx] : symbol;
    }

    private static bool TryParseDateTime(string value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd, HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string cleaned = value.Replace(",", string.Empty).Replace("\"", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}

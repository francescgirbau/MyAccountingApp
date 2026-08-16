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
            string priceStr = fields[8];
            string proceedsStr = fields[10];
            string commissionStr = fields[11];

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
            else if (assetCategory == "Forex")
            {
                CreateForexPair(date, symbol, currency, rawQuantity, priceStr, proceeds, commissionStr, transactions);
            }
        }
    }

    private static void CreateForexPair(DateTime date, string symbol, string currency, decimal quantity, string priceStr, decimal proceeds, string commissionStr, List<Transaction> transactions)
    {
        int separatorIdx = symbol.IndexOf('.');
        bool hasCurrencyPair = separatorIdx > 0
            && separatorIdx < symbol.Length - 1
            && currency.Length == 3;

        if (!hasCurrencyPair || !TryParseDecimal(priceStr, out decimal price) || price <= 0)
        {
            return;
        }

        decimal expectedProceeds = quantity * price;
        if (expectedProceeds == 0 || Math.Abs(proceeds + expectedProceeds) / Math.Abs(expectedProceeds) > ProceedsTolerance)
        {
            return;
        }

        string baseCurrency = symbol[..separatorIdx];
        int quoteStart = separatorIdx + 1;
        string quoteCurrency = symbol[quoteStart..];
        FxLeg baseLeg = quantity > 0 ? FxLeg.In : FxLeg.Out;
        FxLeg quoteLeg = quantity > 0 ? FxLeg.Out : FxLeg.In;
        Guid pairId = Guid.NewGuid();
        string description = $"FX {baseCurrency}→{quoteCurrency} · {symbol}";
        string externalKey = $"{date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}|{symbol}|{quantity.ToString(CultureInfo.InvariantCulture)}|{price.ToString(CultureInfo.InvariantCulture)}";

        transactions.Add(CreateFxLeg(pairId, date, description, new Money(Math.Abs(quantity), baseCurrency), baseLeg, price, externalKey));
        transactions.Add(CreateFxLeg(pairId, date, description, new Money(Math.Abs(proceeds), quoteCurrency), quoteLeg, price, externalKey));

        if (TryParseDecimal(commissionStr, out decimal commission) && commission != 0)
        {
            transactions.Add(new Transaction(date, symbol, new Money(Math.Abs(commission), currency), TransactionCategory.FEE));
        }
    }

    private static Transaction CreateFxLeg(Guid pairId, DateTime date, string description, Money money, FxLeg leg, decimal rate, string externalKey)
    {
        Transaction transaction = new(date, description, money, TransactionCategory.FX_CONVERSION);
        transaction.SetFxPair(pairId, leg, rate, externalKey);
        return transaction;
    }

    private const decimal ProceedsTolerance = 0.02m;

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

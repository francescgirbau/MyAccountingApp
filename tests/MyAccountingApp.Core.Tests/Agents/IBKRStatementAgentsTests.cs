using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Agents;

public class IBKRStatementAgentsTests
{
    [Fact]
    public void TradeAgent_ParsesStockBuyAndSell()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "100", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "MSFT", "2024-12-20, 11:00:00", "-50", string.Empty, string.Empty, "15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Equal(2, assets.Count);
        Assert.Equal(AssetTransactionType.Buy, assets[0].Type);
        Assert.Equal("AAPL", assets[0].Symbol);
        Assert.Equal(100, assets[0].Quantity);
        Assert.Equal(AssetTransactionType.Sell, assets[1].Type);
    }

    [Fact]
    public void TradeAgent_ParsesOptionTransaction()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Equity and Index Options", "USD", "VET 16JAN26 10 C", "2024-12-18, 12:00:00", "1.0", string.Empty, string.Empty, "-110.11", string.Empty, string.Empty, string.Empty, string.Empty },
        };

        agent.Parse(rows, tx, assets, options, errors);

        OptionTransaction option = Assert.Single(options);
        Assert.Equal("VET", option.Symbol);
        Assert.Equal(AssetTransactionType.Buy, option.Type);
        Assert.Empty(assets);
    }

    [Fact]
    public void TradeAgent_SkipsInvalidRows()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "100", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Header", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "100", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Other", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "100", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "0", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "not-a-date", "100", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "100", string.Empty, string.Empty, "0.00", string.Empty, string.Empty, string.Empty, string.Empty },
            new[] { "Trades", "Data", "Order", "Stocks", "USD", "AAPL", "2024-12-19, 10:00:00", "not-qty", string.Empty, string.Empty, "-15000.00", string.Empty, string.Empty, string.Empty, string.Empty },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Empty(assets);
        Assert.Empty(options);
        Assert.Empty(tx);
    }

    [Fact]
    public void CorporateActionAgent_AddsSell_WhenCashProceedsPositive()
    {
        CorporateActionAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Corporate Actions", "Data", "Stocks", "CAD", string.Empty, "2024-07-17", "CVO.ODD.C(CAODD89D1078) Merged", "-33", "203.94", string.Empty },
        };

        agent.Parse(rows, tx, assets, options, errors);

        AssetTransaction asset = Assert.Single(assets);
        Assert.Equal("CVO.ODD.C", asset.Symbol);
        Assert.Equal(AssetTransactionType.Sell, asset.Type);
        Assert.Equal(33, asset.Quantity);
        Assert.Equal(TransactionCategory.INCOME, asset.Transaction.Category);
    }

    [Fact]
    public void CorporateActionAgent_SkipsNoCashOrNegativeRows()
    {
        CorporateActionAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Corporate Actions", "Data", "Stocks", "CAD", string.Empty, "2024-07-17", "No cash", "-33", "0.00", string.Empty },
            new[] { "Corporate Actions", "Data", "Stocks", "CAD", string.Empty, "2024-07-17", "Negative", "-33", "-5.00", string.Empty },
            new[] { "Corporate Actions", "Header", "Stocks", "CAD", string.Empty, "2024-07-17", "Header", "-33", "10.00", string.Empty },
            new[] { "Corporate Actions", "Data", "Stocks", "CAD", string.Empty, "bad-date", "Bad", "-33", "10.00", string.Empty },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Empty(assets);
    }

    [Theory]
    [InlineData("5000.00", 5000, TransactionCategory.DEPOSIT)]
    [InlineData("-2500.00", 2500, TransactionCategory.TRANSFER)]
    public void DepositWithdrawalAgent_ClassifiesBySign(string amount, decimal expectedAmount, TransactionCategory expected)
    {
        DepositWithdrawalAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Deposits & Withdrawals", "Data", "EUR", "2024-12-19", "Bank transfer", amount },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(expected, transaction.Category);
        Assert.Equal(expectedAmount, transaction.Money.Amount);
        Assert.Equal("EUR", transaction.Money.Currency);
    }

    [Fact]
    public void DepositWithdrawalAgent_SkipsInvalidRows()
    {
        DepositWithdrawalAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Deposits & Withdrawals", "Data", "EUR", "bad-date", "Bad", "100.00" },
            new[] { "Deposits & Withdrawals", "Data", "EUR", "2024-12-19", "Zero", "0.00" },
            new[] { "Deposits & Withdrawals", "Header", "EUR", "2024-12-19", "Header", "100.00" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Empty(tx);
    }

    [Theory]
    [InlineData("10.00", TransactionCategory.INTEREST)]
    [InlineData("-2.00", TransactionCategory.EXPENSE)]
    public void InterestAgent_ClassifiesBySign(string amount, TransactionCategory expected)
    {
        InterestAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Interest", "Data", "USD", "2024-12-19", "Interest on cash", amount },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(expected, transaction.Category);
    }

    [Fact]
    public void WithholdingTaxAgent_AddsWithholdingTax()
    {
        WithholdingTaxAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Withholding Tax", "Data", "USD", "2024-12-19", "US tax", "15.00" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.WITHHOLDING_TAX, transaction.Category);
        Assert.Equal(15, transaction.Money.Amount);
    }

    [Fact]
    public void FeeAgent_UsesIndexThreeCurrency()
    {
        FeeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Fees", "Data", "IGNORED", "USD", "2024-12-19", "Commission", "2.50" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.FEE, transaction.Category);
        Assert.Equal("USD", transaction.Money.Currency);
        Assert.Equal(2.5m, transaction.Money.Amount);
    }

    [Fact]
    public void TradeAgent_ParsesForexSell_AsFxPairWithFee()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Forex", "USD", "EUR.USD", "2023-02-02, 11:55:21", "-916", "1.0912", string.Empty, "999.5392", "-1.81986", string.Empty, string.Empty, "0.127654" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Equal(3, tx.Count);
        Assert.Empty(assets);
        Assert.Empty(options);

        Transaction outLeg = tx[0];
        Transaction inLeg = tx[1];
        Assert.Equal(TransactionCategory.FX_CONVERSION, outLeg.Category);
        Assert.Equal(FxLeg.Out, outLeg.FxLeg);
        Assert.Equal(new Money(916, "EUR"), outLeg.Money);
        Assert.Equal(FxLeg.In, inLeg.FxLeg);
        Assert.Equal(new Money(999.5392m, "USD"), inLeg.Money);
        Assert.Equal(outLeg.FxPairId, inLeg.FxPairId);
        Assert.Equal(1.0912m, outLeg.FxBrokerRate);
        Assert.Equal(inLeg.FxBrokerRate, outLeg.FxBrokerRate);
        Assert.Equal("2023-02-02T11:55:21|EUR.USD|-916|1.0912", outLeg.FxExternalKey);
        Assert.Equal(outLeg.FxExternalKey, inLeg.FxExternalKey);
        Assert.Equal("FX EUR→USD · EUR.USD", outLeg.Description);

        Transaction fee = tx[2];
        Assert.Equal(TransactionCategory.FEE, fee.Category);
        Assert.Equal(new Money(1.81986m, "USD"), fee.Money);
    }

    [Fact]
    public void TradeAgent_ParsesForexBuy_AsFxPairWithFee()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Forex", "USD", "EUR.USD", "2023-02-27, 15:14:35", "2", "1.06085", string.Empty, "-2.1217", "-1.89666", string.Empty, string.Empty, "0.000149" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Equal(3, tx.Count);

        Transaction inLeg = tx[0];
        Transaction outLeg = tx[1];
        Assert.Equal(FxLeg.In, inLeg.FxLeg);
        Assert.Equal(new Money(2, "EUR"), inLeg.Money);
        Assert.Equal(FxLeg.Out, outLeg.FxLeg);
        Assert.Equal(new Money(2.1217m, "USD"), outLeg.Money);
        Assert.Equal(inLeg.FxPairId, outLeg.FxPairId);
        Assert.Equal(1.06085m, inLeg.FxBrokerRate);
        Assert.Equal("2023-02-27T15:14:35|EUR.USD|2|1.06085", inLeg.FxExternalKey);
    }

    [Fact]
    public void TradeAgent_ParsesForexCadPair_WithoutFee()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Forex", "CAD", "EUR.CAD", "2023-03-16, 20:36:17", "0.1001", "1.45605", string.Empty, "-0.145750605", "0", string.Empty, string.Empty, "0.000579" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Equal(2, tx.Count);

        Transaction inLeg = tx[0];
        Transaction outLeg = tx[1];
        Assert.Equal(FxLeg.In, inLeg.FxLeg);
        Assert.Equal(new Money(0.1001m, "EUR"), inLeg.Money);
        Assert.Equal(FxLeg.Out, outLeg.FxLeg);
        Assert.Equal(new Money(0.145750605m, "CAD"), outLeg.Money);
        Assert.Equal(inLeg.FxPairId, outLeg.FxPairId);
        Assert.Equal(1.45605m, inLeg.FxBrokerRate);
    }

    [Fact]
    public void TradeAgent_SkipsInvalidForexRows()
    {
        TradeAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Trades", "Data", "Order", "Forex", "USD", "EURUSD", "2023-02-02, 11:55:21", "-916", "1.0912", string.Empty, "999.5392", "-1.81986", string.Empty, string.Empty, "0.127654" },
            new[] { "Trades", "Data", "Order", "Forex", "USD", "EUR.USD", "2023-02-02, 11:55:21", "-916", string.Empty, string.Empty, "999.5392", "-1.81986", string.Empty, string.Empty, "0.127654" },
            new[] { "Trades", "Data", "Order", "Forex", "USD", "EUR.USD", "2023-02-02, 11:55:21", "-916", "1.0912", string.Empty, "500.00", "-1.81986", string.Empty, string.Empty, "0.127654" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Assert.Empty(tx);
        Assert.Empty(assets);
        Assert.Empty(options);
    }

    [Fact]
    public void DividendAgent_AddsIncome()
    {
        DividendAgent agent = new();
        List<Transaction> tx = new();
        List<AssetTransaction> assets = new();
        List<OptionTransaction> options = new();
        List<string> errors = new();

        List<string[]> rows = new()
        {
            new[] { "Dividends", "Data", "USD", "2024-12-19", "AAPL dividend", "1.5" },
        };

        agent.Parse(rows, tx, assets, options, errors);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.DIVIDEND, transaction.Category);
        Assert.Equal(1.5m, transaction.Money.Amount);
    }

    [Fact]
    public void FeeAndDividendAgents_HandleThousandSeparator()
    {
        FeeAgent feeAgent = new();
        List<Transaction> feeTx = new();
        List<string> errors = new();
        List<string[]> feeRows = new()
        {
            new[] { "Fees", "Data", "IGNORED", "USD", "2024-12-19", "Commission", "1,250.00" },
        };
        feeAgent.Parse(feeRows, feeTx, new List<AssetTransaction>(), new List<OptionTransaction>(), errors);
        Assert.Equal(1250m, feeTx.Single().Money.Amount);
    }
}

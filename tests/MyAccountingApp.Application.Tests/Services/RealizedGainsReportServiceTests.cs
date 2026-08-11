using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class RealizedGainsReportServiceTests
{
    private static AssetTransaction Buy(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(Guid.NewGuid(), date, $"Buy {symbol}", money, TransactionCategory.INVESTMENT);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Buy);
    }

    private static AssetTransaction Sell(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(Guid.NewGuid(), date, $"Sell {symbol}", money, TransactionCategory.INCOME);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Sell);
    }

    [Fact]
    public async Task GetRealizedGainsAsync_WithCrossYearSell_ReportsOnlyYearSales()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, new DateTime(2024, 1, 15)));
        repo.AddOrUpdate(Sell("AAPL", 150, 10, new DateTime(2025, 6, 1)));
        RealizedGainsReportService service = new(repo, new InMemoryTransactionRepository());

        RealizedGainsReportDto report = await service.GetRealizedGainsAsync(2025);

        Assert.Equal(2025, report.Year);
        Assert.Equal(500m, report.TotalRealizedGainLoss);
        SymbolRealizedGainsDto symbol = Assert.Single(report.Symbols);
        Assert.Equal("AAPL", symbol.Symbol);
        Assert.Equal("USD", symbol.Currency);
        Assert.Equal(10m, symbol.SoldQuantity);
        Assert.Equal(1500m, symbol.Proceeds);
        Assert.Equal(1000m, symbol.CostBasis);
        Assert.Equal(500m, symbol.RealizedGainLoss);
        RealizedSaleDto sale = Assert.Single(symbol.Sales);
        Assert.Equal(new DateTime(2025, 6, 1), sale.Date);
        Assert.Equal(10m, sale.Quantity);
        Assert.Equal(500m, sale.RealizedGainLoss);
    }

    [Fact]
    public async Task GetRealizedGainsAsync_WithPartialFifoSells_UsesLifoCosts()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, new DateTime(2025, 1, 15)));
        repo.AddOrUpdate(Sell("AAPL", 120, 5, new DateTime(2025, 6, 1)));
        repo.AddOrUpdate(Sell("AAPL", 140, 5, new DateTime(2025, 7, 1)));
        RealizedGainsReportService service = new(repo, new InMemoryTransactionRepository());

        RealizedGainsReportDto report = await service.GetRealizedGainsAsync(2025);

        Assert.Equal(300m, report.TotalRealizedGainLoss);
        SymbolRealizedGainsDto symbol = Assert.Single(report.Symbols);
        Assert.Equal(10m, symbol.SoldQuantity);
        Assert.Equal(1300m, symbol.Proceeds);
        Assert.Equal(1000m, symbol.CostBasis);
        Assert.Equal(2, symbol.Sales.Count);
    }

    [Fact]
    public async Task GetRealizedGainsAsync_WithNoYearSales_ReturnsEmptySymbols()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, new DateTime(2024, 1, 15)));
        RealizedGainsReportService service = new(repo, new InMemoryTransactionRepository());

        RealizedGainsReportDto report = await service.GetRealizedGainsAsync(2025);

        Assert.Empty(report.Symbols);
        Assert.Equal(0m, report.TotalRealizedGainLoss);
    }

    [Fact]
    public async Task GetWithholdingAsync_GroupsByCurrencyAndYear()
    {
        InMemoryTransactionRepository repo = new();
        repo.AddOrUpdate(WithholdingTx(new DateTime(2025, 3, 1), 15m, "USD"));
        repo.AddOrUpdate(WithholdingTx(new DateTime(2025, 4, 1), 20m, "USD"));
        repo.AddOrUpdate(WithholdingTx(new DateTime(2024, 12, 1), 10m, "EUR"));
        repo.AddOrUpdate(new Transaction(Guid.NewGuid(), new DateTime(2025, 5, 1), "Dividend", new Money(50m, "USD"), TransactionCategory.DIVIDEND));
        RealizedGainsReportService service = new(new FakePortfolioRepo(), repo);

        WithholdingReportDto report = await service.GetWithholdingAsync(2025);

        WithholdingTotalDto total = Assert.Single(report.Totals);
        Assert.Equal("USD", total.Currency);
        Assert.Equal(35m, total.Amount);
        Assert.Equal(2, total.TransactionCount);
    }

    private static Transaction WithholdingTx(DateTime date, decimal amount, string currency) =>
        new(Guid.NewGuid(), date, "Withholding", new Money(amount, currency), TransactionCategory.WITHHOLDING_TAX);

    private sealed class FakePortfolioRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void AddOrUpdate(AssetTransaction assetTransaction) =>
            this._transactions.Add(assetTransaction);

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);

        public IEnumerable<AssetTransaction> GetAllTransactions() =>
            this._transactions;

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public bool Delete(Guid transactionId) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}

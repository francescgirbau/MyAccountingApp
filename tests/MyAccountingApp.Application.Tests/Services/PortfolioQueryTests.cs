using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class PortfolioQueryTests
{
    [Fact]
    public void GetPosition_ReturnsNull_WhenNoTransactions()
    {
        FakeRepo repo = new();
        PortfolioQuery query = new(repo);

        var result = query.GetPosition("AAPL");

        Assert.Null(result);
    }

    [Fact]
    public void GetPosition_WithSingleBuy_ReturnsCorrectPosition()
    {
        FakeRepo repo = new();
        repo.AddOrUpdate(MakeBuy("AAPL", 150, 10, new DateTime(2024, 1, 15)));
        PortfolioQuery query = new(repo);

        var result = query.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(10, result.NetQuantity);
        Assert.Equal(150m, result.AverageUnitaryCost);
        Assert.Equal(1500m, result.TotalCostBasis);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(1, result.TransactionCount);
    }

    [Fact]
    public void GetPosition_WithBuyAndSell_CalculatesNetCorrectly()
    {
        FakeRepo repo = new();
        repo.AddOrUpdate(MakeBuy("AAPL", 100, 10, new DateTime(2024, 1, 15)));
        repo.AddOrUpdate(MakeSell("AAPL", 150, 4, new DateTime(2024, 6, 1)));
        PortfolioQuery query = new(repo);

        var result = query.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal(6, result.NetQuantity);
        Assert.Equal(2, result.TransactionCount);
    }

    private static AssetTransaction MakeBuy(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(Guid.NewGuid(), date, $"Buy {symbol}", money, TransactionCategory.EXPENSE);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Buy);
    }

    private static AssetTransaction MakeSell(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(Guid.NewGuid(), date, $"Sell {symbol}", money, TransactionCategory.INCOME);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Sell);
    }

    private sealed class FakeRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void AddOrUpdate(AssetTransaction tx) => this._transactions.Add(tx);
        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);
        public IEnumerable<AssetTransaction> GetAllTransactions() => this._transactions;
        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }
    }
}

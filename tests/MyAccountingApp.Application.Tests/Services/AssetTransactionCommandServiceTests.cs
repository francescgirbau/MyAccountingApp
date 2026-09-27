using MyAccountingApp.Application.Services;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class AssetTransactionCommandServiceTests
{
    [Fact]
    public void PatchMany_ShouldUpdateSymbol_ForAllMatchingIds()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAllTransactions().First(t => t.Transaction.Id == first.Transaction.Id).Symbol);
        Assert.Equal("TSLA", repo.GetAllTransactions().First(t => t.Transaction.Id == second.Transaction.Id).Symbol);
    }

    [Fact]
    public void PatchMany_ShouldReportMissingIds_AsFailures()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, missing },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Asset transaction not found.", failure.Error);
    }

    [Fact]
    public void PatchMany_ShouldReportInvalidSymbol_AsFailure_ForEveryId()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new AssetTransactionPatch(" "));

        Assert.Equal(0, result.Updated);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Contains("cannot be null or empty", failure.Error));
        Assert.Equal("AAPL", repo.GetAllTransactions().First(t => t.Transaction.Id == first.Transaction.Id).Symbol);
        Assert.Equal("AAPL", repo.GetAllTransactions().First(t => t.Transaction.Id == second.Transaction.Id).Symbol);
    }

    [Fact]
    public void PatchMany_ShouldNotCountUnchangedSymbol_AsUpdated()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id },
            new AssetTransactionPatch("AAPL"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("AAPL", repo.GetAllTransactions().Single().Symbol);
    }

    [Fact]
    public void PatchMany_ShouldCountDuplicateIdsOnce()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, existing.Transaction.Id },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAllTransactions().Single().Symbol);
    }

    [Fact]
    public void PatchMany_ShouldReportAllMissingIds_WhenNoneExist()
    {
        FakePfRepo repo = new();
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(new[] { missing }, new AssetTransactionPatch("TSLA"));

        Assert.Equal(0, result.Updated);
        Assert.Single(result.Failures);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void DeleteMany_ShouldRemoveMatchingAssetTransactions()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchDeleteResult result = service.DeleteMany(new[] { first.Transaction.Id, second.Transaction.Id });

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Deleted);
        Assert.Empty(result.Failures);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void DeleteMany_ShouldReportMissingIds_AsFailures()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchDeleteResult result = service.DeleteMany(new[] { existing.Transaction.Id, missing });

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Deleted);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Asset transaction not found.", failure.Error);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void PreviewSplit_ShouldReturnLotsUpToAsOfDate_WithoutMutating()
    {
        FakePfRepo repo = new();
        AssetTransaction early = CreateAsset("CEQ", new DateTime(2023, 2, 13), 1000, 90);
        AssetTransaction middle = CreateAsset("CEQ", new DateTime(2023, 3, 14), 2000, 140);
        AssetTransaction later = CreateAsset("CEQ", new DateTime(2023, 8, 10), 800, 80);
        repo.AddOrUpdate(early);
        repo.AddOrUpdate(middle);
        repo.AddOrUpdate(later);
        AssetTransactionCommandService service = new(repo);

        SplitAdjustmentPreview preview = service.PreviewSplit("CEQ", 0.2m, new DateTime(2023, 6, 30));

        Assert.Equal(2, preview.Items.Count);
        Assert.Equal(new DateTime(2023, 2, 13), preview.Items[0].Date);
        Assert.Equal(1000, preview.Items[0].QuantityBefore);
        Assert.Equal(200, preview.Items[0].QuantityAfter);
        Assert.Equal(0.09m, preview.Items[0].UnitaryCostBefore);
        Assert.Equal(0.45m, preview.Items[0].UnitaryCostAfter);
        Assert.Equal(90, preview.Items[0].Amount);
        Assert.Equal(new DateTime(2023, 3, 14), preview.Items[1].Date);
        Assert.Equal(400, preview.Items[1].QuantityAfter);
        Assert.Equal(0.35m, preview.Items[1].UnitaryCostAfter);
        Assert.Equal(140, preview.Items[1].Amount);

        Assert.Equal(1000, repo.GetAllTransactions().Single(t => t.Transaction.Date == new DateTime(2023, 2, 13)).Quantity);
        Assert.Equal(2000, repo.GetAllTransactions().Single(t => t.Transaction.Date == new DateTime(2023, 3, 14)).Quantity);
        Assert.Equal(800, repo.GetAllTransactions().Single(t => t.Transaction.Date == new DateTime(2023, 8, 10)).Quantity);
    }

    [Fact]
    public void PreviewSplit_ShouldReturnEmpty_WhenNothingMatches()
    {
        FakePfRepo repo = new();
        repo.AddOrUpdate(CreateAsset("AAPL"));
        AssetTransactionCommandService service = new(repo);

        SplitAdjustmentPreview preview = service.PreviewSplit("CEQ", 0.2m, null);

        Assert.Empty(preview.Items);
        Assert.Equal("CEQ", preview.Symbol);
        Assert.Equal(0.2m, preview.Factor);
        Assert.Null(preview.AsOfDate);
    }

    [Fact]
    public void ApplySplit_ShouldAdjustLotsBeforeAsOfDate_AndKeepAmount()
    {
        FakePfRepo repo = new();
        repo.AddOrUpdate(CreateAsset("CEQ", new DateTime(2023, 2, 13), 1000, 90));
        repo.AddOrUpdate(CreateAsset("CEQ", new DateTime(2023, 3, 14), 2000, 140));
        repo.AddOrUpdate(CreateAsset("CEQ", new DateTime(2023, 8, 10), 800, 80));
        AssetTransactionCommandService service = new(repo);

        SplitAdjustmentResult result = service.ApplySplit("CEQ", 0.2m, new DateTime(2023, 6, 30));

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Empty(result.Failures);

        List<AssetTransaction> all = repo.GetAllTransactions().ToList();
        AssetTransaction adj1 = all.Single(t => t.Transaction.Date == new DateTime(2023, 2, 13));
        Assert.Equal(200, adj1.Quantity);
        Assert.Equal(90, adj1.Transaction.Money.Amount);
        Assert.Equal(0.45m, adj1.UnitaryCost().Amount);

        AssetTransaction adj2 = all.Single(t => t.Transaction.Date == new DateTime(2023, 3, 14));
        Assert.Equal(400, adj2.Quantity);
        Assert.Equal(0.35m, adj2.UnitaryCost().Amount);

        AssetTransaction untouched = all.Single(t => t.Transaction.Date == new DateTime(2023, 8, 10));
        Assert.Equal(800, untouched.Quantity);
        Assert.Equal(0.1m, untouched.UnitaryCost().Amount);
    }

    [Fact]
    public void ApplySplit_ShouldOnlyTouchMatchingSymbol()
    {
        FakePfRepo repo = new();
        repo.AddOrUpdate(CreateAsset("CEQ", new DateTime(2023, 2, 13), 1000, 90));
        repo.AddOrUpdate(CreateAsset("AAPL", new DateTime(2023, 3, 14), 1000, 90));
        AssetTransactionCommandService service = new(repo);

        SplitAdjustmentResult result = service.ApplySplit("CEQ", 0.5m, null);

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal(500, repo.GetAllTransactions().Single(t => t.Symbol == "CEQ").Quantity);
        Assert.Equal(1000, repo.GetAllTransactions().Single(t => t.Symbol == "AAPL").Quantity);
    }

    [Fact]
    public void ApplySplit_ShouldReportZeroShareLots_AsFailures()
    {
        FakePfRepo repo = new();
        AssetTransaction tiny = CreateAsset("CEQ", new DateTime(2023, 2, 13), 1, 5);
        repo.AddOrUpdate(tiny);
        AssetTransactionCommandService service = new(repo);

        SplitAdjustmentResult result = service.ApplySplit("CEQ", 0.2m, null);

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal("Split factor leaves no shares on this lot.", failure.Error);
        Assert.Equal(1, repo.GetAllTransactions().Single().Quantity);
    }

    [Fact]
    public void ApplySplit_ShouldThrow_WhenFactorIsNotPositive()
    {
        FakePfRepo repo = new();
        repo.AddOrUpdate(CreateAsset("CEQ", new DateTime(2023, 2, 13), 1000, 90));
        AssetTransactionCommandService service = new(repo);

        Assert.Throws<ArgumentException>(() => service.ApplySplit("CEQ", 0, null));
        Assert.Throws<ArgumentException>(() => service.PreviewSplit("CEQ", -0.5m, null));
    }

    private static AssetTransaction CreateAsset(
        string symbol,
        DateTime? date = null,
        decimal quantity = 2,
        decimal amount = 100)
    {
        Transaction transaction = new(
            Guid.NewGuid(),
            date ?? new DateTime(2026, 8, 1),
            "Test asset",
            new Money(amount, "EUR"),
            TransactionCategory.EXPENSE);
        return new AssetTransaction(transaction, symbol, quantity, AssetTransactionType.Buy);
    }

    private sealed class FakePfRepo : IPortfolioRepository
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

        public bool Delete(Guid transactionId) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}

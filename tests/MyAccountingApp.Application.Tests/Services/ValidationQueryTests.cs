using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class ValidationQueryTests
{
    [Fact]
    public void ValidateAll_ReturnsValid_WhenNoTransactions()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        ValidationQuery query = new(txRepo, pfRepo, validator, new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateAll_CollectsErrors_FromBothRepositories()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();

        Transaction invalidTx = new(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            string.Empty,
            new Money(100, "EUR"),
            TransactionCategory.INCOME);
        txRepo.AddOrUpdate(invalidTx);

        Transaction txForAsset = new(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            string.Empty,
            new Money(100, "EUR"),
            TransactionCategory.INCOME);
        AssetTransaction assetTx = new(txForAsset, "AAPL", 5, AssetTransactionType.Buy);
        pfRepo.AddOrUpdate(assetTx);

        TransactionValidator validator = new();
        ValidationQuery query = new(txRepo, pfRepo, validator, new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void ValidateAll_FlagsFifoShortfall_AsWarning()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2024, 1, 15), "Buy AAPL", new Money(1000m, "USD"), TransactionCategory.INVESTMENT),
            "AAPL",
            10,
            AssetTransactionType.Buy));
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2024, 6, 1), "Sell AAPL", new Money(1500m, "USD"), TransactionCategory.INCOME),
            "AAPL",
            15,
            AssetTransactionType.Sell));
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("FIFO_SHORTFALL", warning.Field);
        Assert.Equal("warning", warning.Severity);
    }

    [Fact]
    public void ValidateAll_FlagsUnmatchedTransfer_AsWarning()
    {
        FakeTxRepo txRepo = new();
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 10), "Transfer to bank", new Money(500m, "EUR"), TransactionCategory.TRANSFER));
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("UNMATCHED_TRANSFER", warning.Field);
    }

    [Fact]
    public void ValidateAll_DoesNotFlagTransfer_WhenDepositPairExists()
    {
        FakeTxRepo txRepo = new();
        DateTime date = new(2025, 1, 10);
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), date, "Transfer to bank", new Money(500m, "EUR"), TransactionCategory.TRANSFER));
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), date.AddDays(1), "Top-up from bank", new Money(500m, "EUR"), TransactionCategory.DEPOSIT));
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.DoesNotContain(result.Warnings, w => w.Field == "UNMATCHED_TRANSFER");
    }

    [Fact]
    public void ValidateAll_FlagsTransferWithSameAmountTransfer_AsUnmatched()
    {
        FakeTxRepo txRepo = new();
        DateTime date = new(2025, 1, 10);
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), date, "Transfer A", new Money(500m, "EUR"), TransactionCategory.TRANSFER));
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), date.AddDays(1), "Transfer B", new Money(500m, "EUR"), TransactionCategory.TRANSFER));
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.Equal(2, result.Warnings.Count(w => w.Field == "UNMATCHED_TRANSFER"));
    }

    [Fact]
    public void ValidateAll_FlagsDuplicateFingerprint_AsError()
    {
        FakeTxRepo txRepo = new();
        Transaction tx = new(Guid.NewGuid(), new DateTime(2025, 1, 10), "Salary", new Money(1000m, "EUR"), TransactionCategory.INCOME);
        txRepo.AddOrUpdate(tx);
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), tx.Date, tx.Description, tx.Money, tx.Category));
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("DUPLICATE_FINGERPRINT", error.Field);
        Assert.Equal("error", error.Severity);
    }

    [Fact]
    public void ValidateAll_FlagsMissingFx_WhenNoConversionForDate()
    {
        FakeTxRepo txRepo = new();
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 10), "Buy in USD", new Money(100m, "USD"), TransactionCategory.EXPENSE));
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("MISSING_FX", warning.Field);
    }

    [Fact]
    public void ValidateAll_DoesNotFlagMissingFx_WhenConversionExists()
    {
        FakeTxRepo txRepo = new();
        DateTime date = new(2025, 1, 10);
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), date, "Buy in USD", new Money(100m, "USD"), TransactionCategory.EXPENSE));
        FakePfRepo pfRepo = new();
        Conversion conversion = new(date, Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.08m } });
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(conversion), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.DoesNotContain(result.Warnings, w => w.Field == "MISSING_FX");
    }

    [Fact]
    public void ValidateAll_DoesNotFlagMissingFx_WhenPreviousRateWithinFiveDays()
    {
        // Arrange: Friday transaction, stored rate from the day before (Thursday).
        FakeTxRepo txRepo = new();
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 10), "Buy in USD", new Money(100m, "USD"), TransactionCategory.EXPENSE));
        FakePfRepo pfRepo = new();
        FakeConversionRepository conversionRepo = new();
        conversionRepo.Initialize(new[] { new Conversion(new DateTime(2025, 1, 9), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.08m } }) });
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), conversionRepo, new FakeMarketPriceService());

        // Act
        ValidationResult result = query.ValidateAll();

        // Assert
        Assert.DoesNotContain(result.Warnings, w => w.Field == "MISSING_FX");
    }

    [Fact]
    public void ValidateAll_FlagsMissingFx_WhenPreviousRateOlderThanFiveDays()
    {
        // Arrange: Friday transaction, stored rate 7 calendar days earlier.
        FakeTxRepo txRepo = new();
        txRepo.AddOrUpdate(new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 10), "Buy in USD", new Money(100m, "USD"), TransactionCategory.EXPENSE));
        FakePfRepo pfRepo = new();
        FakeConversionRepository conversionRepo = new();
        conversionRepo.Initialize(new[] { new Conversion(new DateTime(2025, 1, 3), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.08m } }) });
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), conversionRepo, new FakeMarketPriceService());

        // Act
        ValidationResult result = query.ValidateAll();

        // Assert
        Assert.Contains(result.Warnings, w => w.Field == "MISSING_FX");
    }

    [Fact]
    public void ValidateAll_FlagsSymbolWithoutCachedPrice_AsInfo()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 15), "Buy UNKN", new Money(100m, "USD"), TransactionCategory.INVESTMENT),
            "UNKN",
            5,
            AssetTransactionType.Buy));
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("SYMBOL_NO_PRICE", warning.Field);
        Assert.Equal("info", warning.Severity);
    }

    [Fact]
    public void ValidateAll_FlagsMarketClosed_WhenOnlyStaleQuoteExists()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 15), "Buy AAPL", new Money(100m, "USD"), TransactionCategory.INVESTMENT),
            "AAPL",
            5,
            AssetTransactionType.Buy));
        FakeMarketPriceService priceService = new(new Dictionary<string, Money> { { "AAPL", new Money(150.25m, "USD") } }, new HashSet<string> { "AAPL" });
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), priceService);

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("SYMBOL_MARKET_CLOSED", warning.Field);
        Assert.Equal("info", warning.Severity);
        Assert.Equal("AAPL", warning.Symbol);
        Assert.Contains("last close", warning.Message);
    }

    [Fact]
    public void ValidateAll_DoesNotFlagSymbol_WhenFreshPriceExists()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 15), "Buy AAPL", new Money(100m, "USD"), TransactionCategory.INVESTMENT),
            "AAPL",
            5,
            AssetTransactionType.Buy));
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.DoesNotContain(result.Warnings, w => w.Field == "SYMBOL_NO_PRICE" || w.Field == "SYMBOL_MARKET_CLOSED");
    }

    [Fact]
    public void ValidateAll_DuplicateFingerprint_IncludesAllIdsOfGroup()
    {
        FakeTxRepo txRepo = new();
        Transaction first = new(Guid.NewGuid(), new DateTime(2025, 1, 10), "Salary", new Money(1000m, "EUR"), TransactionCategory.INCOME);
        Transaction second = new(Guid.NewGuid(), first.Date, first.Description, first.Money, first.Category);
        txRepo.AddOrUpdate(first);
        txRepo.AddOrUpdate(second);
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("DUPLICATE_FINGERPRINT", error.Field);
        Assert.Equal(2, error.EntityIds?.Count);
        Assert.Contains(first.Id, error.EntityIds!);
        Assert.Contains(second.Id, error.EntityIds!);
        Assert.Equal($"/transactions?ids={first.Id},{second.Id}", error.DeepLink);
    }

    [Fact]
    public void ValidateAll_UnmatchedTransfer_IncludesTransferId()
    {
        FakeTxRepo txRepo = new();
        Transaction transfer = new(Guid.NewGuid(), new DateTime(2025, 1, 10), "Transfer to bank", new Money(500m, "EUR"), TransactionCategory.TRANSFER);
        txRepo.AddOrUpdate(transfer);
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("UNMATCHED_TRANSFER", warning.Field);
        Assert.Equal("Transaction", warning.EntityType);
        Assert.Equal(transfer.Id, Assert.Single(warning.EntityIds!));
        Assert.Equal($"/transactions?ids={transfer.Id}", warning.DeepLink);
    }

    [Fact]
    public void ValidateAll_MissingFx_IncludesAllGroupedTransactionIds()
    {
        FakeTxRepo txRepo = new();
        DateTime date = new(2025, 1, 10);
        Transaction first = new(Guid.NewGuid(), date, "Buy USD", new Money(100m, "USD"), TransactionCategory.EXPENSE);
        Transaction second = new(Guid.NewGuid(), date, "Fee USD", new Money(5m, "USD"), TransactionCategory.FEE);
        txRepo.AddOrUpdate(first);
        txRepo.AddOrUpdate(second);
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("MISSING_FX", warning.Field);
        Assert.Equal(2, warning.EntityIds?.Count);
        Assert.Contains(first.Id, warning.EntityIds!);
        Assert.Contains(second.Id, warning.EntityIds!);
        Assert.StartsWith("/transactions?ids=", warning.DeepLink);
    }

    [Fact]
    public void ValidateAll_FifoShortfall_HasSymbolAndAssetIds()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        Transaction buyTx = new(Guid.NewGuid(), new DateTime(2024, 1, 15), "Buy AAPL", new Money(1000m, "USD"), TransactionCategory.INVESTMENT);
        Transaction sellTx = new(Guid.NewGuid(), new DateTime(2024, 6, 1), "Sell AAPL", new Money(1500m, "USD"), TransactionCategory.INCOME);
        pfRepo.AddOrUpdate(new AssetTransaction(buyTx, "AAPL", 10, AssetTransactionType.Buy));
        pfRepo.AddOrUpdate(new AssetTransaction(sellTx, "AAPL", 15, AssetTransactionType.Sell));
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("FIFO_SHORTFALL", warning.Field);
        Assert.Equal("AssetTransaction", warning.EntityType);
        Assert.Equal("AAPL", warning.Symbol);
        Assert.Equal(2, warning.EntityIds?.Count);
        Assert.Contains(buyTx.Id, warning.EntityIds!);
        Assert.Contains(sellTx.Id, warning.EntityIds!);
    }

    [Fact]
    public void ValidateAll_FieldErrors_CarryTransactionIdAndDeepLink()
    {
        FakeTxRepo txRepo = new();
        Transaction tx = new(Guid.NewGuid(), DateTime.UtcNow.AddDays(10), "Future purchase", new Money(100m, "EUR"), TransactionCategory.EXPENSE);
        txRepo.AddOrUpdate(tx);
        FakePfRepo pfRepo = new();
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("Date", error.Field);
        Assert.Equal("Transaction", error.EntityType);
        Assert.Equal(tx.Id, Assert.Single(error.EntityIds!));
        Assert.Equal($"/transactions?ids={tx.Id}", error.DeepLink);
    }

    [Fact]
    public void ValidateAll_SymbolNoPrice_HasSymbolDeepLinkWhenNoIds()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.AddOrUpdate(new AssetTransaction(
            new Transaction(Guid.NewGuid(), new DateTime(2025, 1, 15), "Buy UNKN", new Money(100m, "USD"), TransactionCategory.INVESTMENT),
            "UNKN",
            5,
            AssetTransactionType.Buy));
        ValidationQuery query = new(txRepo, pfRepo, new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("SYMBOL_NO_PRICE", warning.Field);
        Assert.Equal("AssetTransaction", warning.EntityType);
        Assert.Equal("UNKN", warning.Symbol);
        Assert.NotNull(warning.DeepLink);
        Assert.Equal("/asset-transactions?symbol=UNKN", warning.DeepLink);
    }

    [Fact]
    public void ValidateAll_DoesNotFlagUnmatchedFx_WhenPairComplete()
    {
        FakeTxRepo txRepo = new();
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(490.24m, "EUR"), TransactionCategory.FX_CONVERSION);
        outLeg.SetFxPair(pairId, FxLeg.Out, 1.1121m);
        Transaction inLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(545.20m, "USD"), TransactionCategory.FX_CONVERSION);
        inLeg.SetFxPair(pairId, FxLeg.In, 1.1121m);
        txRepo.AddOrUpdate(outLeg);
        txRepo.AddOrUpdate(inLeg);
        ValidationQuery query = new(txRepo, new FakePfRepo(), new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        Assert.DoesNotContain(result.Warnings, w => w.Field == "UNMATCHED_FX");
        Assert.DoesNotContain(result.Warnings, w => w.Field == "MISSING_FX");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateAll_FlagsOrphanFxLeg_AsWarningWithDeepLink()
    {
        FakeTxRepo txRepo = new();
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(490.24m, "EUR"), TransactionCategory.FX_CONVERSION);
        outLeg.SetFxPair(pairId, FxLeg.Out);
        txRepo.AddOrUpdate(outLeg);
        ValidationQuery query = new(txRepo, new FakePfRepo(), new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings);
        Assert.Equal("UNMATCHED_FX", warning.Field);
        Assert.Equal("warning", warning.Severity);
        Assert.Equal("Transaction", warning.EntityType);
        Assert.Equal(outLeg.Id, Assert.Single(warning.EntityIds!));
        Assert.Equal($"/transactions?ids={outLeg.Id}", warning.DeepLink);
    }

    [Fact]
    public void ValidateAll_MissingFx_DoesNotFlagFxLegs_ButFlagsNonFxNonEur()
    {
        FakeTxRepo txRepo = new();
        Guid pairId = Guid.NewGuid();
        Transaction fxOutLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(490.24m, "EUR"), TransactionCategory.FX_CONVERSION);
        fxOutLeg.SetFxPair(pairId, FxLeg.Out);
        Transaction fxInLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(545.20m, "USD"), TransactionCategory.FX_CONVERSION);
        fxInLeg.SetFxPair(pairId, FxLeg.In);
        Transaction dividend = new(new DateTime(2025, 1, 10), "MSFT dividend", new Money(3.10m, "USD"), TransactionCategory.DIVIDEND);
        txRepo.AddOrUpdate(fxOutLeg);
        txRepo.AddOrUpdate(fxInLeg);
        txRepo.AddOrUpdate(dividend);
        ValidationQuery query = new(txRepo, new FakePfRepo(), new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError warning = Assert.Single(result.Warnings, w => w.Field == "MISSING_FX");
        Assert.Equal(dividend.Id, Assert.Single(warning.EntityIds!));
        Assert.DoesNotContain(fxInLeg.Id, warning.EntityIds!);
        Assert.DoesNotContain(result.Warnings, w => w.Field == "UNMATCHED_FX");
    }

    [Fact]
    public void ValidateAll_FlagsFxWithoutPairOrLeg_AsError()
    {
        FakeTxRepo txRepo = new();
        Transaction tx = new(new DateTime(2025, 1, 10), "Corrupted FX", new Money(100m, "USD"), TransactionCategory.FX_CONVERSION);
        txRepo.AddOrUpdate(tx);
        ValidationQuery query = new(txRepo, new FakePfRepo(), new TransactionValidator(), new FakeConversionRepo(), new FakeMarketPriceService());

        ValidationResult result = query.ValidateAll();

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("FxPairId", error.Field);
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);
        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public IEnumerable<Transaction> GetAll() => this._transactions;
        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
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

    private sealed class FakeConversionRepo : IConversionRepository
    {
        private readonly Conversion? _conversion;

        public FakeConversionRepo(Conversion? conversion = null)
        {
            this._conversion = conversion;
        }

        public void AddOrUpdate(Conversion conversion)
        {
        }

        public Conversion? GetByDate(DateTime date) => this._conversion;

        public Conversion? GetLatestOnOrBefore(DateTime date) => this._conversion;

        public void Initialize(IEnumerable<Conversion> conversions)
        {
        }

        public IEnumerable<Conversion> GetAll() => this._conversion is null ? Array.Empty<Conversion>() : new[] { this._conversion };
    }
}

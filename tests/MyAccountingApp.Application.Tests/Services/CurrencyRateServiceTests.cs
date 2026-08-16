using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class CurrencyRateServiceTests
{
    [Fact]
    public async Task GetConversionAsync_ReturnsCached_WhenExists_WithoutConsumingQuota()
    {
        // Arrange
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2005, 12, 1));

        // Assert
        Assert.False(result.IsStale);
        Assert.Equal(0, quota.Consumed);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task GetConversionAsync_FetchesFromApi_WhenMissingAndQuotaAvailable()
    {
        // Arrange
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2023, 12, 1));

        // Assert
        Assert.False(result.IsStale);
        Assert.Equal(1, quota.Consumed);
        Assert.Equal(1.1m, result.Quotes[Currencies.USD]);
        Assert.NotNull(repo.GetByDate(new DateTime(2023, 12, 1)));
    }

    [Fact]
    public async Task GetConversionAsync_ReturnsStaleFallback_WhenMissingAndNoQuota()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        FakeCurrencyConverter converter = new();
        CurrencyRateService service = new(repo, converter, Currencies.EUR, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2023, 12, 1));

        // Assert
        Assert.True(result.IsStale);
        Assert.Equal(0, quota.Consumed);
        Assert.Equal(0, converter.FetchAllCalls);
        Assert.Contains(new DateOnly(2023, 12, 1), queue.Enqueued);
        Assert.Equal(new DateTime(2023, 11, 29), result.Date);
        Assert.Equal(1.1m, result.Quotes[Currencies.USD]);
    }

    [Fact]
    public async Task GetConversionAsync_Throws_WhenFallbackIsOlderThanFiveDays()
    {
        // Arrange: stored rate is 11 calendar days before the requested date.
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 20), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = new(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);

        // Act & Assert
        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => service.GetConversionAsync(new DateTime(2023, 12, 1)));
        Assert.Contains(new DateOnly(2023, 12, 1), queue.Enqueued);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task GetConversionAsync_Throws_WhenMissingNoQuotaAndNoHistory()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act & Assert
        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => service.GetConversionAsync(new DateTime(2023, 12, 1)));
    }

    [Fact]
    public async Task SyncRangeAsync_SavesAllDatesAndConsumesOneRequest()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        bool result = await service.SyncRangeAsync(new DateOnly(2023, 12, 1), new DateOnly(2023, 12, 5));

        // Assert
        Assert.True(result);
        Assert.Equal(1, quota.Consumed);
        Assert.Equal(5, repo.GetAll().Count());
    }

    [Fact]
    public async Task SyncRangeAsync_ReturnsFalse_WhenNoQuota()
    {
        // Arrange
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        FakeCurrencyConverter converter = new();
        CurrencyRateService service = new(repo, converter, Currencies.EUR, quota, queue);

        // Act
        bool result = await service.SyncRangeAsync(new DateOnly(2023, 12, 1), new DateOnly(2023, 12, 5));

        // Assert
        Assert.False(result);
        Assert.Equal(0, quota.Consumed);
        Assert.Equal(0, converter.FetchRangeCalls);
    }

    [Fact]
    public async Task SyncRangeAsync_DoesNotConsumeQuota_WhenApiFails()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = new(repo, new FailingCurrencyConverter(), Currencies.EUR, quota, queue);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.SyncRangeAsync(new DateOnly(2023, 12, 1), new DateOnly(2023, 12, 5)));
        Assert.Equal(0, quota.Consumed);
        Assert.False(quota.Exhausted);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public async Task ProcessPendingAsync_DoesNotConsumeQuota_WhenRangeFetchFails()
    {
        // Arrange
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = new(repo, new FailingCurrencyConverter(), Currencies.EUR, quota, queue);

        await queue.EnqueueAsync(new DateOnly(2023, 12, 1));
        await queue.EnqueueAsync(new DateOnly(2023, 12, 2));

        // Act
        PendingProcessingResult result = await service.ProcessPendingAsync();

        // Assert
        Assert.Equal(0, result.RequestsSpent);
        Assert.Equal(0, result.ProcessedDays);
        Assert.Equal(1, result.Failures);
        Assert.Equal(0, quota.Consumed);
        Assert.False(quota.Exhausted);
    }

    [Fact]
    public async Task ProcessPendingAsync_GroupsContiguousDates_IntoSingleRequest()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        for (int day = 1; day <= 5; day++)
        {
            await queue.EnqueueAsync(new DateOnly(2023, 12, day));
        }

        // Act
        PendingProcessingResult result = await service.ProcessPendingAsync();

        // Assert
        Assert.Equal(1, result.RequestsSpent);
        Assert.Equal(5, result.ProcessedDays);
        Assert.Equal(5, repo.GetAll().Count());
    }

    [Fact]
    public async Task ProcessPendingAsync_SplitsNonContiguousDates_IntoMultipleRequests()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        await queue.EnqueueAsync(new DateOnly(2023, 12, 1));
        await queue.EnqueueAsync(new DateOnly(2023, 12, 2));
        await queue.EnqueueAsync(new DateOnly(2023, 12, 10));
        await queue.EnqueueAsync(new DateOnly(2023, 12, 11));

        // Act
        PendingProcessingResult result = await service.ProcessPendingAsync();

        // Assert
        Assert.Equal(2, result.RequestsSpent);
        Assert.Equal(4, result.ProcessedDays);
    }

    [Fact]
    public async Task ProcessPendingAsync_StopsWhenQuotaExhausted()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new() { MaxConsumptions = 1 };
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        await queue.EnqueueAsync(new DateOnly(2023, 12, 1));
        await queue.EnqueueAsync(new DateOnly(2023, 12, 10));

        // Act
        PendingProcessingResult result = await service.ProcessPendingAsync();

        // Assert
        Assert.Equal(1, result.RequestsSpent);
        Assert.Equal(1, result.ProcessedDays);

        IReadOnlyList<PendingConversionRequest> remaining = await queue.GetPendingAsync();
        Assert.Single(remaining);
        Assert.Equal(new DateOnly(2023, 12, 10), remaining[0].Date);
    }

    [Fact]
    public async Task SyncDatesAsync_GroupsContiguousDates_IntoSingleRequest()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        int synced = await service.SyncDatesAsync(new[] { new DateOnly(2023, 12, 1), new DateOnly(2023, 12, 2), new DateOnly(2023, 12, 10) });

        // Assert
        Assert.Equal(3, synced);
        Assert.Equal(2, quota.Consumed);
        Assert.Equal(3, repo.GetAll().Count());
    }

    [Fact]
    public async Task SyncDatesAsync_ReturnsZero_WhenNoDates()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        int synced = await service.SyncDatesAsync(Array.Empty<DateOnly>());

        // Assert
        Assert.Equal(0, synced);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task BackfillIfEmptyAsync_FetchesRange_WhenRepoEmpty()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        bool result = await service.BackfillIfEmptyAsync(3);

        // Assert
        Assert.True(result);
        Assert.Equal(1, quota.Consumed);
        Assert.Equal(3, repo.GetAll().Count());
    }

    [Fact]
    public async Task BackfillIfEmptyAsync_DoesNothing_WhenRepoNotEmpty()
    {
        // Arrange
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(new FakeConversionRepository(), quota, queue);

        // Act
        bool result = await service.BackfillIfEmptyAsync(3);

        // Assert
        Assert.False(result);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task GetQuotaAsync_ReturnsQuotaFromManager()
    {
        // Arrange
        FakeApiQuotaManager quota = new();
        CurrencyRateService service = CreateService(new FakeConversionRepository(), quota, new FakePendingConversionQueue());

        // Act
        ApiUsageQuota result = await service.GetQuotaAsync();

        // Assert
        Assert.Equal("test", result.Provider);
        Assert.Equal(0, result.RequestsUsed);
        Assert.Equal(90, result.Available);
    }

    [Fact]
    public async Task GetConversionAsync_FallsBackToStale_WithoutConsumingQuota_WhenApiFails()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = new(repo, new FailingCurrencyConverter(), Currencies.EUR, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2023, 12, 1));

        // Assert
        Assert.True(result.IsStale);
        Assert.Equal(0, quota.Consumed);
        Assert.False(quota.Exhausted);
        Assert.Contains(new DateOnly(2023, 12, 1), queue.Enqueued);
        Assert.Equal(new DateTime(2023, 11, 29), result.Date);
    }

    [Fact]
    public async Task GetConversionAsync_MarksExhausted_WithoutConsuming_WhenApiReportsQuotaExceeded()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = new(repo, new QuotaExceededCurrencyConverter(), Currencies.EUR, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2023, 12, 1));

        // Assert
        Assert.True(result.IsStale);
        Assert.True(quota.Exhausted);
        Assert.Equal(0, quota.Consumed);
        Assert.Contains(new DateOnly(2023, 12, 1), queue.Enqueued);
        Assert.Equal(new DateTime(2023, 11, 29), result.Date);
    }

    [Fact]
    public async Task SyncGapAsync_FillsGapFromLastCachedToYesterday()
    {
        // Arrange
        DateTime seedDate = DateTime.UtcNow.Date.AddDays(-3);
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(seedDate, Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        int synced = await service.SyncGapAsync(365);

        // Assert
        Assert.Equal(2, synced);
        Assert.Equal(1, quota.Consumed);
        Assert.Equal(3, repo.GetAll().Count());
    }

    [Fact]
    public async Task SyncGapAsync_ReturnsZero_WhenRepositoryEmpty()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        int synced = await service.SyncGapAsync(365);

        // Assert
        Assert.Equal(0, synced);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task SyncGapAsync_ReturnsZero_WhenCacheIsUpToDate()
    {
        // Arrange
        DateTime seedDate = DateTime.UtcNow.Date.AddDays(-1);
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(seedDate, Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        // Act
        int synced = await service.SyncGapAsync(365);

        // Assert
        Assert.Equal(0, synced);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsProviderCachedDaysLastSyncAndPending()
    {
        // Arrange
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);
        await queue.EnqueueAsync(new DateOnly(2023, 12, 1));

        // Act
        ConversionStatus status = await service.GetStatusAsync();

        // Assert
        Assert.Equal("frankfurter", status.Provider);
        Assert.Equal(1, status.CachedDays);
        Assert.Equal(new DateTime(2005, 12, 1), status.LastCachedDate);
        Assert.Equal(1, status.PendingCount);
    }

    [Fact]
    public async Task GetFxQuotesAsync_ReturnsRequestedAndRateDates_WhenFresh()
    {
        FakeConversionRepository repo = new();
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        IReadOnlyList<FxQuoteDto> quotes = await service.GetFxQuotesAsync(new DateTime(2023, 12, 1));

        FxQuoteDto usd = Assert.Single(quotes, q => q.Quote == "USD");
        Assert.Equal(new DateOnly(2023, 12, 1), usd.RequestedDate);
        Assert.Equal(new DateOnly(2023, 12, 1), usd.RateDate);
        Assert.False(usd.IsStale);
        Assert.Equal(1.1m, usd.Rate);
        Assert.Equal("EUR", usd.Base);
    }

    [Fact]
    public async Task GetFxQuotesAsync_ExposesFallbackRateDate_WhenStale()
    {
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        IReadOnlyList<FxQuoteDto> quotes = await service.GetFxQuotesAsync(new DateTime(2023, 12, 1));

        FxQuoteDto usd = Assert.Single(quotes, q => q.Quote == "USD");
        Assert.Equal(new DateOnly(2023, 12, 1), usd.RequestedDate);
        Assert.Equal(new DateOnly(2023, 11, 29), usd.RateDate);
        Assert.True(usd.IsStale);
        Assert.Equal(1.1m, usd.Rate);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task GetFxQuotesAsync_DoesNotCorruptCachedEntity_WhenStale()
    {
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurrencyRateService service = CreateService(repo, quota, queue);

        await service.GetFxQuotesAsync(new DateTime(2023, 12, 1));

        Conversion cached = repo.GetByDate(new DateTime(2023, 11, 29)) !;
        Assert.False(cached.IsStale);
        Assert.Equal(new DateTime(2023, 11, 29), cached.Date);
    }

    private static CurrencyRateService CreateService(
        FakeConversionRepository repo,
        FakeApiQuotaManager quota,
        FakePendingConversionQueue queue)
    {
        return new CurrencyRateService(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
    }

    private sealed class FailingCurrencyConverter : ICurrencyConverter
    {
        public Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
        {
            throw new HttpRequestException("provider unavailable");
        }

        public Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
            Currencies source,
            DateOnly start,
            DateOnly end,
            IReadOnlyCollection<Currencies>? targets = null,
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("provider unavailable");
        }
    }

    private sealed class QuotaExceededCurrencyConverter : ICurrencyConverter
    {
        public Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
        {
            throw new CurrencyApiQuotaExceededException("provider reported quota exceeded");
        }

        public Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
            Currencies source,
            DateOnly start,
            DateOnly end,
            IReadOnlyCollection<Currencies>? targets = null,
            CancellationToken cancellationToken = default)
        {
            throw new CurrencyApiQuotaExceededException("provider reported quota exceeded");
        }
    }
}

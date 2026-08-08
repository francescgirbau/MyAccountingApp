using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
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
        CurencyRateService service = CreateService(repo, quota, queue);

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
        CurencyRateService service = CreateService(repo, quota, queue);

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
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurencyRateService service = CreateService(repo, quota, queue);

        // Act
        Conversion result = await service.GetConversionAsync(new DateTime(2023, 12, 1));

        // Assert
        Assert.True(result.IsStale);
        Assert.Equal(0, quota.Consumed);
        Assert.Contains(new DateOnly(2023, 12, 1), queue.Enqueued);
        Assert.Equal(new DateTime(2005, 12, 1), result.Date);
        Assert.Equal(1.1m, result.Quotes[Currencies.USD]);
    }

    [Fact]
    public async Task GetConversionAsync_Throws_WhenMissingNoQuotaAndNoHistory()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurencyRateService service = CreateService(repo, quota, queue);

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
        CurencyRateService service = CreateService(repo, quota, queue);

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
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        FakePendingConversionQueue queue = new();
        CurencyRateService service = CreateService(new FakeConversionRepository(), quota, queue);

        // Act
        bool result = await service.SyncRangeAsync(new DateOnly(2023, 12, 1), new DateOnly(2023, 12, 5));

        // Assert
        Assert.False(result);
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public async Task ProcessPendingAsync_GroupsContiguousDates_IntoSingleRequest()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurencyRateService service = CreateService(repo, quota, queue);

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
        CurencyRateService service = CreateService(repo, quota, queue);

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
        CurencyRateService service = CreateService(repo, quota, queue);

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
    public async Task BackfillIfEmptyAsync_FetchesRange_WhenRepoEmpty()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        FakePendingConversionQueue queue = new();
        CurencyRateService service = CreateService(repo, quota, queue);

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
        CurencyRateService service = CreateService(new FakeConversionRepository(), quota, queue);

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
        CurencyRateService service = CreateService(new FakeConversionRepository(), quota, new FakePendingConversionQueue());

        // Act
        ApiUsageQuota result = await service.GetQuotaAsync();

        // Assert
        Assert.Equal("test", result.Provider);
        Assert.Equal(0, result.RequestsUsed);
        Assert.Equal(90, result.Available);
    }

    private static CurencyRateService CreateService(
        FakeConversionRepository repo,
        FakeApiQuotaManager quota,
        FakePendingConversionQueue queue)
    {
        return new CurencyRateService(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
    }
}

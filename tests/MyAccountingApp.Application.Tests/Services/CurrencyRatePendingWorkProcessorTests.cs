using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class CurrencyRatePendingWorkProcessorTests
{
    private static PendingWorkRequest Request(DateOnly date)
    {
        return new PendingWorkRequest(PendingWorkOperations.CurrencyRate, CurrencyRatePendingWorkPayload.Create(date), DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessAsync_GroupsContiguousDates_IntoSingleRequest()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        CurrencyRatePendingWorkProcessor processor = new(repo, new FakeCurrencyConverter(), quota, Currencies.EUR, "frankfurter");

        List<PendingWorkRequest> requests = Enumerable.Range(1, 5)
            .Select(day => Request(new DateOnly(2023, 12, day)))
            .ToList();

        // Act
        PendingWorkProcessingResult result = await processor.ProcessAsync(requests);

        // Assert
        Assert.Equal(1, result.RequestsSpent);
        Assert.Equal(5, result.DaysSynced);
        Assert.Equal(5, result.Processed.Count);
        Assert.Equal(1.1m, Assert.Single(repo.GetAll(), c => c.Date.Date == new DateTime(2023, 12, 1)).Quotes[Currencies.USD]);
    }

    [Fact]
    public async Task ProcessAsync_SplitsNonContiguousDates_IntoMultipleRequests()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        CurrencyRatePendingWorkProcessor processor = new(repo, new FakeCurrencyConverter(), quota, Currencies.EUR, "frankfurter");

        List<PendingWorkRequest> requests = new[]
        {
            new DateOnly(2023, 12, 1),
            new DateOnly(2023, 12, 2),
            new DateOnly(2023, 12, 10),
            new DateOnly(2023, 12, 11),
        }.Select(Request).ToList();

        // Act
        PendingWorkProcessingResult result = await processor.ProcessAsync(requests);

        // Assert
        Assert.Equal(2, result.RequestsSpent);
        Assert.Equal(4, result.DaysSynced);
        Assert.Equal(4, result.Processed.Count);
    }

    [Fact]
    public async Task ProcessAsync_StopsWhenQuotaExhausted()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new() { MaxConsumptions = 1 };
        CurrencyRatePendingWorkProcessor processor = new(repo, new FakeCurrencyConverter(), quota, Currencies.EUR, "frankfurter");

        List<PendingWorkRequest> requests = new[]
        {
            new DateOnly(2023, 12, 1),
            new DateOnly(2023, 12, 10),
        }.Select(Request).ToList();

        // Act
        PendingWorkProcessingResult result = await processor.ProcessAsync(requests);

        // Assert
        Assert.Equal(1, result.RequestsSpent);
        Assert.Equal(1, result.DaysSynced);
        PendingWorkRequest processed = Assert.Single(result.Processed);
        Assert.Equal(new DateOnly(2023, 12, 1), CurrencyRatePendingWorkPayload.ReadDate(processed));
    }

    [Fact]
    public async Task ProcessAsync_ReturnsNothing_WhenRangeFetchFails()
    {
        // Arrange
        FakeConversionRepository repo = new();
        repo.Initialize(Array.Empty<Conversion>());
        FakeApiQuotaManager quota = new();
        CurrencyRatePendingWorkProcessor processor = new(repo, new FailingConverter(), quota, Currencies.EUR, "frankfurter");

        List<PendingWorkRequest> requests = new[]
        {
            new DateOnly(2023, 12, 1),
            new DateOnly(2023, 12, 2),
        }.Select(Request).ToList();

        // Act
        PendingWorkProcessingResult result = await processor.ProcessAsync(requests);

        // Assert
        Assert.Empty(result.Processed);
        Assert.Equal(0, result.RequestsSpent);
        Assert.Equal(0, result.DaysSynced);
        Assert.Equal(0, quota.Consumed);
        Assert.False(quota.Exhausted);
    }

    [Fact]
    public async Task ProcessAsync_EmptyBatch_ReturnsEmptyResult()
    {
        // Arrange
        CurrencyRatePendingWorkProcessor processor = new(
            new FakeConversionRepository(),
            new FakeCurrencyConverter(),
            new FakeApiQuotaManager(),
            Currencies.EUR,
            "frankfurter");

        // Act
        PendingWorkProcessingResult result = await processor.ProcessAsync(Array.Empty<PendingWorkRequest>());

        // Assert
        Assert.Empty(result.Processed);
        Assert.Equal(0, result.RequestsSpent);
        Assert.Equal(0, result.DaysSynced);
    }

    [Fact]
    public void Payload_RoundTripsDate()
    {
        // Arrange
        DateOnly date = new(2023, 12, 1);
        PendingWorkRequest request = Request(date);

        // Act
        DateOnly read = CurrencyRatePendingWorkPayload.ReadDate(request);

        // Assert
        Assert.Equal(date, read);
    }

    private sealed class FailingConverter : ICurrencyConverter
    {
        // Fake that always throws on range fetches, like the rate-service test counterpart.
        public Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
            Currencies source,
            DateOnly start,
            DateOnly end,
            IReadOnlyCollection<Currencies>? targets = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("API is down");
        }

        public Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
        {
            return Task.FromResult(new Dictionary<string, decimal>());
        }
    }
}
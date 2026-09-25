using Microsoft.Extensions.Logging.Abstractions;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PendingWorkDispatcherTests
{
    private const string Operation = "FakeOperation";

    private static PendingWorkOptions Options(int maxAttempts = 3, int maxItems = 100)
    {
        return new PendingWorkOptions
        {
            BaseRetryDelayMinutes = 5,
            MaxAttempts = maxAttempts,
            MaxItemsPerRun = maxItems,
        };
    }

    [Fact]
    public async Task RunOperationAsync_ProcessesDueItems_AndMarksProcessed()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[]
        {
            new PendingWorkRequest(Operation, "due-failed", now, status: PendingStatus.Failed, attempts: 1, nextRetryAtUtc: now.AddMinutes(-1)),
            new PendingWorkRequest(Operation, "not-yet-due", now, status: PendingStatus.Failed, attempts: 1, nextRetryAtUtc: now.AddMinutes(30)),
            new PendingWorkRequest(Operation, "pending", now),
        });
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: true) },
            Options(),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult result = await dispatcher.RunOperationAsync(Operation);

        // Assert
        Assert.Equal(2, result.ProcessedItems);
        Assert.Equal(0, result.FailedItems);
        Assert.Equal(PendingStatus.Processed, repo.GetAll().Single(r => r.PayloadJson == "due-failed").Status);
        Assert.Equal(PendingStatus.Processed, repo.GetAll().Single(r => r.PayloadJson == "pending").Status);
        Assert.Equal(PendingStatus.Failed, repo.GetAll().Single(r => r.PayloadJson == "not-yet-due").Status);
        Assert.Null(repo.GetAll().Single(r => r.PayloadJson == "due-failed").NextRetryAtUtc);
    }

    [Fact]
    public async Task RunOperationAsync_MarksFailedWithBackoff_WhenProcessorFails()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[] { new PendingWorkRequest(Operation, "a", now) });
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: false) },
            Options(),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult result = await dispatcher.RunOperationAsync(Operation);

        // Assert
        Assert.Equal(0, result.ProcessedItems);
        Assert.Equal(1, result.FailedItems);
        PendingWorkRequest item = repo.GetAll().Single();
        Assert.Equal(PendingStatus.Failed, item.Status);
        Assert.Equal(1, item.Attempts);
        Assert.Equal(now.AddMinutes(5), item.NextRetryAtUtc);
    }

    [Fact]
    public async Task RunOperationAsync_BackoffDoublesPerAttempt()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[] { new PendingWorkRequest(Operation, "a", now) });
        FakeTimeProvider time = new(new DateTimeOffset(now, TimeSpan.Zero));
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: false) },
            Options(),
            time,
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        await dispatcher.RunOperationAsync(Operation);
        PendingWorkRequest first = repo.GetAll().Single();
        int firstAttempts = first.Attempts;
        DateTime firstNextRetry = first.NextRetryAtUtc!.Value;
        time.Now = new DateTimeOffset(now.AddMinutes(5), TimeSpan.Zero);
        await dispatcher.RunOperationAsync(Operation);
        PendingWorkRequest second = repo.GetAll().Single();

        // Assert
        Assert.Equal(1, firstAttempts);
        Assert.Equal(now.AddMinutes(5), firstNextRetry);
        Assert.Equal(2, second.Attempts);
        Assert.Equal(now.AddMinutes(5).AddMinutes(10), second.NextRetryAtUtc);
    }

    [Fact]
    public async Task RunOperationAsync_GivesUp_AfterMaxAttempts()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[]
        {
            new PendingWorkRequest(Operation, "a", now, status: PendingStatus.Failed, attempts: 2, nextRetryAtUtc: now.AddMinutes(-1)),
        });
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: false) },
            Options(maxAttempts: 3),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult first = await dispatcher.RunOperationAsync(Operation);
        PendingWorkRunResult second = await dispatcher.RunOperationAsync(Operation);

        // Assert
        Assert.Equal(1, first.FailedItems);
        PendingWorkRequest item = repo.GetAll().Single();
        Assert.Equal(3, item.Attempts);
        Assert.Null(item.NextRetryAtUtc);
        Assert.Equal(0, second.ProcessedItems + second.FailedItems);
    }

    [Fact]
    public async Task RunOperationAsync_RespectsMaxItemsPerRun()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[]
        {
            new PendingWorkRequest(Operation, "a", now),
            new PendingWorkRequest(Operation, "b", now),
            new PendingWorkRequest(Operation, "c", now),
        });
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: true) },
            Options(maxItems: 2),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult result = await dispatcher.RunOperationAsync(Operation);

        // Assert
        Assert.Equal(2, result.ProcessedItems);
        Assert.Equal(1, repo.GetAll().Count(r => r.Status == PendingStatus.Pending));
    }

    [Fact]
    public async Task RunOperationAsync_UnknownOperation_ReturnsEmpty()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor(Operation, succeed: true) },
            Options(),
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult result = await dispatcher.RunOperationAsync("Unknown");

        // Assert
        Assert.Equal(0, result.ProcessedItems + result.FailedItems + result.RequestsSpent + result.DaysSynced);
    }

    [Fact]
    public async Task RunAllAsync_IteratesEveryProcessor()
    {
        // Arrange
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        FakePendingWorkRepository repo = new();
        repo.Initialize(new[]
        {
            new PendingWorkRequest("A", "a", now),
            new PendingWorkRequest("B", "b", now),
        });
        PendingWorkDispatcher dispatcher = new(
            new PendingWorkQueue(repo),
            new[] { new FakeProcessor("A", succeed: true), new FakeProcessor("B", succeed: false) },
            Options(),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)),
            NullLogger<PendingWorkDispatcher>.Instance);

        // Act
        PendingWorkRunResult result = await dispatcher.RunAllAsync();

        // Assert
        Assert.Equal(1, result.ProcessedItems);
        Assert.Equal(1, result.FailedItems);
        Assert.Equal(PendingStatus.Processed, repo.GetAll().Single(r => r.Operation == "A").Status);
        Assert.Equal(PendingStatus.Failed, repo.GetAll().Single(r => r.Operation == "B").Status);
    }

    private sealed class FakeProcessor : IPendingWorkProcessor
    {
        private readonly bool _succeed;

        public FakeProcessor(string operation, bool succeed)
        {
            this.Operation = operation;
            this._succeed = succeed;
        }

        public string Operation { get; }

        public Task<PendingWorkProcessingResult> ProcessAsync(
            IReadOnlyList<PendingWorkRequest> requests,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PendingWorkRequest> processed = this._succeed ? requests : Array.Empty<PendingWorkRequest>();
            return Task.FromResult(new PendingWorkProcessingResult(processed, requests.Count, requests.Count));
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public FakeTimeProvider(DateTimeOffset now)
        {
            this.Now = now;
        }

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return this.Now;
        }
    }
}
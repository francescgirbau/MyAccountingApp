using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PendingWorkQueueTests
{
    private const string Operation = PendingWorkOperations.CurrencyRate;
    private static readonly string Payload = CurrencyRatePendingWorkPayload.Create(new DateOnly(2026, 7, 1));

    [Fact]
    public async Task EnqueueAsync_AddsRequest_WhenNewPayload()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);

        // Act
        await queue.EnqueueAsync(Operation, Payload);

        // Assert
        IReadOnlyList<PendingWorkRequest> pending = await queue.GetPendingAsync(Operation);
        PendingWorkRequest item = Assert.Single(pending);
        Assert.Equal(Operation, item.Operation);
        Assert.Equal(Payload, item.PayloadJson);
        Assert.Equal(PendingStatus.Pending, item.Status);
    }

    [Fact]
    public async Task EnqueueAsync_DoesNotDuplicate_WhenSameOperationAndPayload()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);

        // Act
        await queue.EnqueueAsync(Operation, Payload);
        await queue.EnqueueAsync(Operation, Payload);

        // Assert
        IReadOnlyList<PendingWorkRequest> pending = await queue.GetPendingAsync(Operation);
        Assert.Single(pending);
    }

    [Fact]
    public async Task EnqueueAsync_AllowsDifferentPayloadsForSameOperation()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);

        // Act
        await queue.EnqueueAsync(Operation, CurrencyRatePendingWorkPayload.Create(new DateOnly(2026, 7, 1)));
        await queue.EnqueueAsync(Operation, CurrencyRatePendingWorkPayload.Create(new DateOnly(2026, 7, 2)));

        // Assert
        IReadOnlyList<PendingWorkRequest> pending = await queue.GetPendingAsync(Operation);
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task MarkProcessedAsync_RemovesFromPending()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);
        await queue.EnqueueAsync(Operation, Payload);

        // Act
        PendingWorkRequest item = Assert.Single(await queue.GetPendingAsync(Operation));
        await queue.MarkProcessedAsync(item.Id);

        // Assert
        Assert.Empty(await queue.GetPendingAsync(Operation));
        Assert.Equal(PendingStatus.Processed, repo.GetAll().Single().Status);
    }

    [Fact]
    public async Task MarkFailedAsync_RecordsErrorAttemptAndNextRetry()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);
        await queue.EnqueueAsync(Operation, Payload);
        PendingWorkRequest item = Assert.Single(await queue.GetPendingAsync(Operation));
        DateTime failedAt = new(2026, 7, 1, 12, 0, 0);

        // Act
        await queue.MarkFailedAsync(item.Id, "boom", TimeSpan.FromMinutes(5), failedAt, maxAttempts: 3);

        // Assert
        PendingWorkRequest request = repo.GetAll().Single();
        Assert.Equal(PendingStatus.Failed, request.Status);
        Assert.Equal("boom", request.LastError);
        Assert.Equal(1, request.Attempts);
        Assert.Equal(new DateTime(2026, 7, 1, 12, 5, 0), request.NextRetryAtUtc);
        Assert.Single(await queue.GetPendingAsync(Operation));
    }

    [Fact]
    public async Task MarkFailedAsync_GivesUp_WhenMaxAttemptsReached()
    {
        // Arrange
        FakePendingWorkRepository repo = new();
        PendingWorkQueue queue = new(repo);
        await queue.EnqueueAsync(Operation, Payload);
        PendingWorkRequest item = Assert.Single(await queue.GetPendingAsync(Operation));
        DateTime failedAt = new(2026, 7, 1, 12, 0, 0);

        // Act
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await queue.MarkFailedAsync(item.Id, "boom", TimeSpan.FromMinutes(5), failedAt, maxAttempts: 3);
        }

        // Assert
        PendingWorkRequest request = repo.GetAll().Single();
        Assert.Equal(3, request.Attempts);
        Assert.Null(request.NextRetryAtUtc);
        Assert.Empty(await queue.GetPendingAsync(Operation));
    }
}
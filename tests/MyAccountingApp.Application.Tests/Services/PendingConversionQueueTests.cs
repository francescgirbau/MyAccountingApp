using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PendingConversionQueueTests
{
    [Fact]
    public async Task EnqueueAsync_AddsRequest_WhenNewDate()
    {
        // Arrange
        FakePendingConversionRepository repo = new();
        PendingConversionQueue queue = new(repo);

        // Act
        await queue.EnqueueAsync(new DateOnly(2026, 7, 1));

        // Assert
        IReadOnlyList<PendingConversionRequest> pending = await queue.GetPendingAsync();
        PendingConversionRequest item = Assert.Single(pending);
        Assert.Equal(new DateOnly(2026, 7, 1), item.Date);
        Assert.Equal(PendingStatus.Pending, item.Status);
    }

    [Fact]
    public async Task EnqueueAsync_DoesNotDuplicate_WhenSameDate()
    {
        // Arrange
        FakePendingConversionRepository repo = new();
        PendingConversionQueue queue = new(repo);

        // Act
        await queue.EnqueueAsync(new DateOnly(2026, 7, 1));
        await queue.EnqueueAsync(new DateOnly(2026, 7, 1));

        // Assert
        IReadOnlyList<PendingConversionRequest> pending = await queue.GetPendingAsync();
        Assert.Single(pending);
    }

    [Fact]
    public async Task MarkProcessedAsync_RemovesFromPending()
    {
        // Arrange
        FakePendingConversionRepository repo = new();
        PendingConversionQueue queue = new(repo);
        await queue.EnqueueAsync(new DateOnly(2026, 7, 1));

        // Act
        await queue.MarkProcessedAsync(new DateOnly(2026, 7, 1));

        // Assert
        IReadOnlyList<PendingConversionRequest> pending = await queue.GetPendingAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkFailedAsync_RecordsError()
    {
        // Arrange
        FakePendingConversionRepository repo = new();
        PendingConversionQueue queue = new(repo);
        await queue.EnqueueAsync(new DateOnly(2026, 7, 1));

        // Act
        await queue.MarkFailedAsync(new DateOnly(2026, 7, 1), "boom");

        // Assert
        PendingConversionRequest request = repo.GetAll().Single();
        Assert.Equal(PendingStatus.Failed, request.Status);
        Assert.Equal("boom", request.LastError);
    }
}

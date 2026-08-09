using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Tests.Services;

public class PendingConversionQueueTests : IDisposable
{
    private readonly string _tempFile;

    public PendingConversionQueueTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public async Task EnqueueAsync_AddsRequest_WhenNewDate()
    {
        // Arrange
        JsonPendingConversionRepository repo = new(this._tempFile);
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
        JsonPendingConversionRepository repo = new(this._tempFile);
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
        JsonPendingConversionRepository repo = new(this._tempFile);
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
        JsonPendingConversionRepository repo = new(this._tempFile);
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

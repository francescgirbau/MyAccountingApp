using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// In-memory fake of the pending conversion queue for testing.
/// </summary>
public class FakePendingConversionQueue : IPendingConversionQueue
{
    private readonly List<PendingConversionRequest> _requests = new();

    /// <summary>
    /// Gets the set of dates enqueued.
    /// </summary>
    public IReadOnlyCollection<DateOnly> Enqueued => this._requests.Select(r => r.Date).ToList();

    /// <inheritdoc/>
    public Task EnqueueAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (this._requests.All(r => r.Date != date))
        {
            this._requests.Add(new PendingConversionRequest(date, Currencies.EUR, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PendingConversionRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        List<PendingConversionRequest> pending = this._requests.Where(r => r.Status == PendingStatus.Pending).ToList();
        return Task.FromResult<IReadOnlyList<PendingConversionRequest>>(pending);
    }

    /// <inheritdoc/>
    public Task MarkProcessedAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        PendingConversionRequest? request = this._requests.FirstOrDefault(r => r.Date == date);

        if (request != null)
        {
            request.MarkProcessed(DateTime.UtcNow);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkFailedAsync(DateOnly date, string error, CancellationToken cancellationToken = default)
    {
        PendingConversionRequest? request = this._requests.FirstOrDefault(r => r.Date == date);

        if (request != null)
        {
            request.MarkFailed(error);
        }

        return Task.CompletedTask;
    }
}

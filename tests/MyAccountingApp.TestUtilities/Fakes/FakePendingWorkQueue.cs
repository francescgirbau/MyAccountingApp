using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// In-memory fake of the generic pending work queue for testing.
/// </summary>
public class FakePendingWorkQueue : IPendingWorkQueue
{
    private readonly List<PendingWorkRequest> _requests = new();

    /// <summary>
    /// Gets all stored requests.
    /// </summary>
    public IReadOnlyList<PendingWorkRequest> Requests => this._requests;

    /// <summary>
    /// Gets the set of dates enqueued for the currency rate operation.
    /// </summary>
    public IReadOnlyCollection<DateOnly> Enqueued => this._requests
        .Where(r => r.Operation == PendingWorkOperations.CurrencyRate)
        .Select(CurrencyRatePendingWorkPayload.ReadDate)
        .ToList();

    /// <inheritdoc/>
    public Task EnqueueAsync(string operation, string payloadJson, CancellationToken cancellationToken = default)
    {
        bool exists = this._requests.Any(r =>
            r.Operation == operation
            && r.PayloadJson == payloadJson
            && r.Status != PendingStatus.Processed);

        if (!exists)
        {
            this._requests.Add(new PendingWorkRequest(operation, payloadJson, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PendingWorkRequest>> GetPendingAsync(string operation, CancellationToken cancellationToken = default)
    {
        List<PendingWorkRequest> pending = this._requests
            .Where(r => r.Operation == operation
                && (r.Status == PendingStatus.Pending
                    || (r.Status == PendingStatus.Failed && r.NextRetryAtUtc != null)))
            .ToList();

        return Task.FromResult<IReadOnlyList<PendingWorkRequest>>(pending);
    }

    /// <inheritdoc/>
    public Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? request = this._requests.FirstOrDefault(r => r.Id == id);

        if (request != null)
        {
            request.MarkProcessing();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? request = this._requests.FirstOrDefault(r => r.Id == id);

        if (request != null)
        {
            request.MarkProcessed(DateTime.UtcNow);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan retryDelay,
        DateTime failedAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? request = this._requests.FirstOrDefault(r => r.Id == id);

        if (request != null)
        {
            request.MarkFailed(error, failedAtUtc, retryDelay, maxAttempts);
        }

        return Task.CompletedTask;
    }
}
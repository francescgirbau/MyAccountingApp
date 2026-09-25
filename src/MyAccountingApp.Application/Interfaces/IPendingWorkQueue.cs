using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Manages a generic queue of offline work items waiting to be processed, with
/// retry bookkeeping (attempts and next-retry timestamps) on failure.
/// </summary>
public interface IPendingWorkQueue
{
    /// <summary>
    /// Enqueues a work item for the given operation if no equivalent item is already queued.
    /// </summary>
    /// <param name="operation">The operation that processes the item.</param>
    /// <param name="payloadJson">The operation-specific payload serialized as JSON.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnqueueAsync(string operation, string payloadJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the items waiting to be processed for the given operation: pending items and
    /// failed items that are still retryable.
    /// </summary>
    /// <param name="operation">The operation to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The pending (or retryable) items for the operation.</returns>
    Task<IReadOnlyList<PendingWorkRequest>> GetPendingAsync(string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the item as currently being processed, preventing concurrent claims.
    /// </summary>
    /// <param name="id">The identifier of the item.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the item as processed.
    /// </summary>
    /// <param name="id">The identifier of the item.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the item as failed, incrementing its attempt counter and scheduling a retry
    /// unless the maximum number of attempts has been reached.
    /// </summary>
    /// <param name="id">The identifier of the item.</param>
    /// <param name="error">A description of the failure.</param>
    /// <param name="retryDelay">Delay until the next retry.</param>
    /// <param name="failedAtUtc">The UTC timestamp of the failure.</param>
    /// <param name="maxAttempts">Maximum number of processing attempts before giving up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan retryDelay,
        DateTime failedAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
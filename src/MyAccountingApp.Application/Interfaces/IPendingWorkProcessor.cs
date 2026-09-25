using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// The outcome of processing a batch of pending work items.
/// </summary>
/// <param name="Processed">The requests that were processed successfully.</param>
/// <param name="RequestsSpent">The number of external API requests consumed.</param>
/// <param name="DaysSynced">The number of calendar days persisted (where applicable).</param>
public sealed record PendingWorkProcessingResult(
    IReadOnlyList<PendingWorkRequest> Processed,
    int RequestsSpent,
    int DaysSynced);

/// <summary>
/// Processes queued work items for a single operation.
/// </summary>
public interface IPendingWorkProcessor
{
    /// <summary>
    /// Gets the name of the operation this processor handles.
    /// </summary>
    string Operation { get; }

    /// <summary>
    /// Processes a batch of pending work requests, returning the subset that succeeded.
    /// Requests not returned are considered failed and will be retried by the queue.
    /// </summary>
    /// <param name="requests">The requests to process.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The outcome of the processing run.</returns>
    Task<PendingWorkProcessingResult> ProcessAsync(
        IReadOnlyList<PendingWorkRequest> requests,
        CancellationToken cancellationToken = default);
}
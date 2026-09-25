namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// The outcome of a pending work dispatch run for one or more operations.
/// </summary>
/// <param name="ProcessedItems">The number of items marked as processed.</param>
/// <param name="FailedItems">The number of items marked as failed.</param>
/// <param name="RequestsSpent">The number of external API requests consumed.</param>
/// <param name="DaysSynced">The number of calendar days persisted (where applicable).</param>
public sealed record PendingWorkRunResult(int ProcessedItems, int FailedItems, int RequestsSpent, int DaysSynced);

/// <summary>
/// Orchestrates processing of the generic pending work queue: claims due items per operation,
/// hands them to the registered processor and applies retry bookkeeping on failure.
/// </summary>
public interface IPendingWorkDispatcher
{
    /// <summary>
    /// Runs a single dispatch pass for the given operation.
    /// </summary>
    /// <param name="operation">The operation to process.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The outcome of the run.</returns>
    Task<PendingWorkRunResult> RunOperationAsync(string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a dispatch pass for every registered operation.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The combined outcome of the run.</returns>
    Task<PendingWorkRunResult> RunAllAsync(CancellationToken cancellationToken = default);
}
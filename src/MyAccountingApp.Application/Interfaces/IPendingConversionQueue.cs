using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Manages a queue of conversion dates waiting to be fetched when API quota becomes available.
/// </summary>
public interface IPendingConversionQueue
{
    /// <summary>
    /// Enqueues a date for later processing if it is not already queued.
    /// </summary>
    /// <param name="date">The date to enqueue.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnqueueAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the requests currently waiting to be processed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The list of pending requests.</returns>
    Task<IReadOnlyList<PendingConversionRequest>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the request for the given date as processed.
    /// </summary>
    /// <param name="date">The processed date.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkProcessedAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the request for the given date as failed.
    /// </summary>
    /// <param name="date">The failed date.</param>
    /// <param name="error">A description of the failure.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkFailedAsync(DateOnly date, string error, CancellationToken cancellationToken = default);
}

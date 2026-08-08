using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Manages the monthly request quota against the external currency API.
/// </summary>
public interface IApiQuotaManager
{
    /// <summary>
    /// Gets the current quota, resetting the period if a new month has started.
    /// </summary>
    /// <returns>The current quota.</returns>
    Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to consume the given number of requests from the quota.
    /// </summary>
    /// <param name="cost">The number of requests the operation would consume.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the quota was consumed; otherwise, false.</returns>
    Task<bool> TryConsumeAsync(int cost = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the quota as exhausted, stopping any further consumption.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkExhaustedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the quota reflects the current period, resetting usage when a new month begins.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnsurePeriodAsync(CancellationToken cancellationToken = default);
}

namespace MyAccountingApp.Application.Options;

/// <summary>
/// Configuration options for the generic offline pending work queue with background retry.
/// </summary>
public sealed class PendingWorkOptions
{
    /// <summary>
    /// Gets or sets how often the background worker checks the queue for due items.
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the base retry delay in minutes; each additional attempt doubles the delay.
    /// </summary>
    public int BaseRetryDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of processing attempts before an item is given up on.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of items processed per run per operation.
    /// </summary>
    public int MaxItemsPerRun { get; set; } = 100;
}
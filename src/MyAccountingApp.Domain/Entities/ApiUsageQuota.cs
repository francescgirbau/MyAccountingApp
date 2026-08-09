namespace MyAccountingApp.Domain.Entities;

/// <summary>
/// Tracks the monthly request usage against the external currency API.
/// </summary>
public sealed class ApiUsageQuota
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiUsageQuota"/> class.
    /// </summary>
    /// <param name="provider">The name of the external provider.</param>
    /// <param name="periodStart">The first day of the current quota period.</param>
    /// <param name="periodEnd">The last day of the current quota period.</param>
    /// <param name="requestsUsed">The number of requests already consumed in the period.</param>
    /// <param name="requestsLimit">The monthly request limit.</param>
    /// <param name="safetyMargin">The number of requests reserved as a safety margin.</param>
    /// <param name="updatedAtUtc">The UTC timestamp of the last update.</param>
    public ApiUsageQuota(
        string provider,
        DateOnly periodStart,
        DateOnly periodEnd,
        int requestsUsed,
        int requestsLimit,
        int safetyMargin,
        DateTime updatedAtUtc)
    {
        this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.PeriodStart = periodStart;
        this.PeriodEnd = periodEnd;
        this.RequestsUsed = requestsUsed;
        this.RequestsLimit = requestsLimit;
        this.SafetyMargin = safetyMargin;
        this.UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Gets the name of the external provider.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the first day of the current quota period.
    /// </summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>
    /// Gets the last day of the current quota period.
    /// </summary>
    public DateOnly PeriodEnd { get; private set; }

    /// <summary>
    /// Gets the number of requests already consumed in the period.
    /// </summary>
    public int RequestsUsed { get; private set; }

    /// <summary>
    /// Gets the monthly request limit.
    /// </summary>
    public int RequestsLimit { get; }

    /// <summary>
    /// Gets the number of requests reserved as a safety margin.
    /// </summary>
    public int SafetyMargin { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the number of requests available for consumption.
    /// </summary>
    public int Available => Math.Max(0, this.RequestsLimit - this.SafetyMargin - this.RequestsUsed);

    /// <summary>
    /// Determines whether the given request cost can be consumed without exceeding the quota.
    /// </summary>
    /// <param name="cost">The number of requests the operation would consume.</param>
    /// <returns>True if the quota can absorb the cost; otherwise, false.</returns>
    public bool CanConsume(int cost = 1)
    {
        return this.Available >= cost;
    }

    /// <summary>
    /// Registers usage of the given number of requests.
    /// </summary>
    /// <param name="cost">The number of requests consumed.</param>
    public void RegisterUsage(int cost = 1)
    {
        this.RequestsUsed = Math.Min(this.RequestsLimit, this.RequestsUsed + cost);
        this.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the quota as exhausted so no further requests are attempted.
    /// </summary>
    public void MarkExhausted()
    {
        this.RequestsUsed = this.RequestsLimit;
        this.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the quota when the current period has ended and a new period begins.
    /// </summary>
    /// <param name="today">The current date.</param>
    public void EnsureCurrentPeriod(DateOnly today)
    {
        if (today > this.PeriodEnd)
        {
            this.PeriodStart = new DateOnly(today.Year, today.Month, 1);
            this.PeriodEnd = this.PeriodStart.AddMonths(1).AddDays(-1);
            this.RequestsUsed = 0;
            this.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

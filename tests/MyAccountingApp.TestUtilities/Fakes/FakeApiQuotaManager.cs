using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// Fake quota manager for testing, with configurable availability.
/// </summary>
public class FakeApiQuotaManager : IApiQuotaManager
{
    /// <summary>
    /// Gets or sets a value indicating whether quota consumption is allowed.
    /// </summary>
    public bool CanConsumeResult { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of consumptions allowed before the quota is exhausted.
    /// </summary>
    public int? MaxConsumptions { get; set; }

    /// <summary>
    /// Gets the total number of requests consumed.
    /// </summary>
    public int Consumed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the quota was marked as exhausted.
    /// </summary>
    public bool Exhausted { get; private set; }

    /// <inheritdoc/>
    public Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        ApiUsageQuota quota = new("test", today, today.AddMonths(1).AddDays(-1), this.Consumed, 100, 10, DateTime.UtcNow);
        return Task.FromResult(quota);
    }

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(int cost = 1, CancellationToken cancellationToken = default)
    {
        if (this.MaxConsumptions.HasValue && this.Consumed >= this.MaxConsumptions.Value)
        {
            return Task.FromResult(false);
        }

        if (!this.CanConsumeResult)
        {
            return Task.FromResult(false);
        }

        this.Consumed += cost;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task MarkExhaustedAsync(CancellationToken cancellationToken = default)
    {
        this.Exhausted = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnsurePeriodAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

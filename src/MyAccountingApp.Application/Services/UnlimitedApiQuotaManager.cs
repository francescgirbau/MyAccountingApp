using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Manages the request quota against the external currency API.
/// </summary>
public class UnlimitedApiQuotaManager : IApiQuotaManager
{
    private readonly string _providerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlimitedApiQuotaManager"/> class.
    /// </summary>
    /// <param name="providerName">The name of the external provider.</param>
    public UnlimitedApiQuotaManager(string providerName)
    {
        this._providerName = providerName;
    }

    /// <inheritdoc/>
    public Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly periodStart = new(today.Year, today.Month, 1);
        ApiUsageQuota quota = new(this._providerName, periodStart, periodStart.AddMonths(1).AddDays(-1), 0, int.MaxValue, 0, DateTime.UtcNow);
        return Task.FromResult(quota);
    }

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(int cost = 1, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task MarkExhaustedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnsurePeriodAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

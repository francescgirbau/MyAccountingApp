using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Manages the monthly request quota against the external currency API.
/// </summary>
public class ApiQuotaManager : IApiQuotaManager
{
    private readonly IApiQuotaRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiQuotaManager"/> class.
    /// </summary>
    /// <param name="repository">The repository backing the quota.</param>
    public ApiQuotaManager(IApiQuotaRepository repository)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc/>
    public Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        ApiUsageQuota quota = this.GetCurrentQuota();
        return Task.FromResult(quota);
    }

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(int cost = 1, CancellationToken cancellationToken = default)
    {
        ApiUsageQuota quota = this.GetCurrentQuota();

        if (!quota.CanConsume(cost))
        {
            return Task.FromResult(false);
        }

        quota.RegisterUsage(cost);
        this._repository.Save(quota);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task MarkExhaustedAsync(CancellationToken cancellationToken = default)
    {
        ApiUsageQuota quota = this.GetCurrentQuota();
        quota.MarkExhausted();
        this._repository.Save(quota);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnsurePeriodAsync(CancellationToken cancellationToken = default)
    {
        ApiUsageQuota quota = this.GetCurrentQuota();
        this._repository.Save(quota);
        return Task.CompletedTask;
    }

    private ApiUsageQuota GetCurrentQuota()
    {
        ApiUsageQuota quota = this._repository.Get();
        quota.EnsureCurrentPeriod(DateOnly.FromDateTime(DateTime.UtcNow.Date));
        return quota;
    }
}

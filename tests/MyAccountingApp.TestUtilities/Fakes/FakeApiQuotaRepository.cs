using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// In-memory fake of the currency API quota repository for testing.
/// </summary>
public class FakeApiQuotaRepository : IApiQuotaRepository
{
    private ApiUsageQuota? _quota;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeApiQuotaRepository"/> class.
    /// </summary>
    /// <param name="requestsLimit">The monthly request limit.</param>
    /// <param name="safetyMargin">The number of requests reserved as a safety margin.</param>
    /// <param name="providerName">The name of the external provider.</param>
    public FakeApiQuotaRepository(int requestsLimit = 100, int safetyMargin = 10, string providerName = "exchangerate.host")
    {
        this.RequestsLimit = requestsLimit;
        this.SafetyMargin = safetyMargin;
        this.ProviderName = providerName;
    }

    private int RequestsLimit { get; }

    private int SafetyMargin { get; }

    private string ProviderName { get; }

    /// <summary>
    /// Gets the current quota, creating a default quota for the current month if none is stored.
    /// </summary>
    /// <returns>The current quota.</returns>
    public ApiUsageQuota Get()
    {
        return this._quota ?? this.CreateDefault();
    }

    /// <summary>
    /// Saves the quota to in-memory storage.
    /// </summary>
    /// <param name="quota">The quota to save.</param>
    public void Save(ApiUsageQuota quota)
    {
        this._quota = quota;
    }

    private ApiUsageQuota CreateDefault()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly periodStart = new(today.Year, today.Month, 1);
        DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return new ApiUsageQuota(this.ProviderName, periodStart, periodEnd, 0, this.RequestsLimit, this.SafetyMargin, DateTime.UtcNow);
    }
}

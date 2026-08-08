using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Defines storage for the currency API usage quota.
/// </summary>
public interface IApiQuotaRepository
{
    /// <summary>
    /// Gets the current quota, creating a default quota if none is stored.
    /// </summary>
    /// <returns>The current quota.</returns>
    ApiUsageQuota Get();

    /// <summary>
    /// Saves the quota to persistent storage.
    /// </summary>
    /// <param name="quota">The quota to save.</param>
    void Save(ApiUsageQuota quota);
}

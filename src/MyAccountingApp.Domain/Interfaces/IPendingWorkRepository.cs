using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Defines storage for queued work requests.
/// </summary>
public interface IPendingWorkRepository
{
    /// <summary>
    /// Gets all queued work requests.
    /// </summary>
    /// <returns>All queued work requests.</returns>
    IEnumerable<PendingWorkRequest> GetAll();

    /// <summary>
    /// Adds a new request or updates an existing request.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    void AddOrUpdate(PendingWorkRequest request);

    /// <summary>
    /// Replaces all stored requests with the given collection.
    /// </summary>
    /// <param name="requests">The requests to store.</param>
    void Initialize(IEnumerable<PendingWorkRequest> requests);
}
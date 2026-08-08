using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Defines storage for queued conversion requests.
/// </summary>
public interface IPendingConversionRepository
{
    /// <summary>
    /// Gets all queued conversion requests.
    /// </summary>
    /// <returns>All queued conversion requests.</returns>
    IEnumerable<PendingConversionRequest> GetAll();

    /// <summary>
    /// Adds a new request or updates an existing request.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    void AddOrUpdate(PendingConversionRequest request);

    /// <summary>
    /// Replaces all stored requests with the given collection.
    /// </summary>
    /// <param name="requests">The requests to store.</param>
    void Initialize(IEnumerable<PendingConversionRequest> requests);
}

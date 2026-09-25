using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// In-memory fake of the pending work repository for testing.
/// </summary>
public class FakePendingWorkRepository : IPendingWorkRepository
{
    private readonly List<PendingWorkRequest> _requests = new();

    /// <summary>
    /// Gets all queued work requests.
    /// </summary>
    /// <returns>All queued work requests.</returns>
    public IEnumerable<PendingWorkRequest> GetAll()
    {
        return this._requests;
    }

    /// <summary>
    /// Adds a new request or updates an existing request.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    public void AddOrUpdate(PendingWorkRequest request)
    {
        int index = this._requests.FindIndex(r => r.Id == request.Id);
        if (index >= 0)
        {
            this._requests[index] = request;
        }
        else
        {
            this._requests.Add(request);
        }
    }

    /// <summary>
    /// Replaces all stored requests with the given collection.
    /// </summary>
    /// <param name="requests">The requests to store.</param>
    public void Initialize(IEnumerable<PendingWorkRequest> requests)
    {
        this._requests.Clear();
        this._requests.AddRange(requests);
    }
}
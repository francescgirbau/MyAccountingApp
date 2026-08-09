using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

/// <summary>
/// In-memory fake of the pending conversion repository for testing.
/// </summary>
public class FakePendingConversionRepository : IPendingConversionRepository
{
    private readonly List<PendingConversionRequest> _requests = new();

    /// <summary>
    /// Gets all queued conversion requests.
    /// </summary>
    /// <returns>All queued conversion requests.</returns>
    public IEnumerable<PendingConversionRequest> GetAll()
    {
        return this._requests;
    }

    /// <summary>
    /// Adds a new request or updates an existing request.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    public void AddOrUpdate(PendingConversionRequest request)
    {
        int index = this._requests.FindIndex(r => r.Date == request.Date);
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
    public void Initialize(IEnumerable<PendingConversionRequest> requests)
    {
        this._requests.Clear();
        this._requests.AddRange(requests);
    }
}

using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Manages a generic queue of offline work items waiting to be processed when API quota
/// becomes available, with retry bookkeeping on failure.
/// </summary>
public class PendingWorkQueue : IPendingWorkQueue
{
    private readonly IPendingWorkRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingWorkQueue"/> class.
    /// </summary>
    /// <param name="repository">The repository backing the queue.</param>
    public PendingWorkQueue(IPendingWorkRepository repository)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(string operation, string payloadJson, CancellationToken cancellationToken = default)
    {
        bool exists = this._repository.GetAll().Any(r =>
            r.Operation == operation
            && r.PayloadJson == payloadJson
            && r.Status != PendingStatus.Processed);

        if (!exists)
        {
            PendingWorkRequest request = new(operation, payloadJson, DateTime.UtcNow);
            this._repository.AddOrUpdate(request);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PendingWorkRequest>> GetPendingAsync(string operation, CancellationToken cancellationToken = default)
    {
        List<PendingWorkRequest> pending = this._repository.GetAll()
            .Where(r => r.Operation == operation
                && (r.Status == PendingStatus.Pending
                    || (r.Status == PendingStatus.Failed && r.NextRetryAtUtc != null)))
            .ToList();

        return Task.FromResult<IReadOnlyList<PendingWorkRequest>>(pending);
    }

    /// <inheritdoc/>
    public Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? existing = this.GetById(id);

        if (existing != null)
        {
            existing.MarkProcessing();
            this._repository.AddOrUpdate(existing);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? existing = this.GetById(id);

        if (existing != null)
        {
            existing.MarkProcessed(DateTime.UtcNow);
            this._repository.AddOrUpdate(existing);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan retryDelay,
        DateTime failedAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        PendingWorkRequest? existing = this.GetById(id);

        if (existing != null)
        {
            existing.MarkFailed(error, failedAtUtc, retryDelay, maxAttempts);
            this._repository.AddOrUpdate(existing);
        }

        return Task.CompletedTask;
    }

    private PendingWorkRequest? GetById(Guid id)
    {
        return this._repository.GetAll().FirstOrDefault(r => r.Id == id);
    }
}
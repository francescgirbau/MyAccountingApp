using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Manages a queue of conversion dates waiting to be fetched when API quota becomes available.
/// </summary>
public class PendingConversionQueue : IPendingConversionQueue
{
    private readonly IPendingConversionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingConversionQueue"/> class.
    /// </summary>
    /// <param name="repository">The repository backing the queue.</param>
    public PendingConversionQueue(IPendingConversionRepository repository)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        PendingConversionRequest? existing = this._repository.GetAll().FirstOrDefault(r => r.Date == date);

        if (existing == null)
        {
            this._repository.AddOrUpdate(new PendingConversionRequest(date, Currencies.EUR, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PendingConversionRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        List<PendingConversionRequest> pending = this._repository.GetAll()
            .Where(r => r.Status == PendingStatus.Pending)
            .ToList();
        return Task.FromResult<IReadOnlyList<PendingConversionRequest>>(pending);
    }

    /// <inheritdoc/>
    public Task MarkProcessedAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        PendingConversionRequest? existing = this._repository.GetAll().FirstOrDefault(r => r.Date == date);

        if (existing != null)
        {
            existing.MarkProcessed(DateTime.UtcNow);
            this._repository.AddOrUpdate(existing);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkFailedAsync(DateOnly date, string error, CancellationToken cancellationToken = default)
    {
        PendingConversionRequest? existing = this._repository.GetAll().FirstOrDefault(r => r.Date == date);

        if (existing != null)
        {
            existing.MarkFailed(error);
            this._repository.AddOrUpdate(existing);
        }

        return Task.CompletedTask;
    }
}

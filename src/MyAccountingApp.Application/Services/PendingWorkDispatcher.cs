using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Orchestrates processing of the generic pending work queue: claims due items per
/// operation, hands them to the registered processor and applies retry bookkeeping
/// (exponential backoff, max attempts) on failure.
/// </summary>
public sealed class PendingWorkDispatcher : IPendingWorkDispatcher
{
    private readonly IPendingWorkQueue _queue;
    private readonly IReadOnlyDictionary<string, IPendingWorkProcessor> _processors;
    private readonly PendingWorkOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PendingWorkDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingWorkDispatcher"/> class.
    /// </summary>
    /// <param name="queue">The pending work queue.</param>
    /// <param name="processors">The processors registered for each operation.</param>
    /// <param name="options">Retry and batching options.</param>
    /// <param name="timeProvider">Time provider used for due checks and retry scheduling.</param>
    /// <param name="logger">Logger for structured observability of the dispatch runs.</param>
    public PendingWorkDispatcher(
        IPendingWorkQueue queue,
        IEnumerable<IPendingWorkProcessor> processors,
        PendingWorkOptions options,
        TimeProvider timeProvider,
        ILogger<PendingWorkDispatcher> logger)
    {
        this._queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this._processors = (processors ?? throw new ArgumentNullException(nameof(processors)))
            .ToDictionary(p => p.Operation);
        this._options = options ?? throw new ArgumentNullException(nameof(options));
        this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this._logger = logger ?? NullLogger<PendingWorkDispatcher>.Instance;
    }

    /// <inheritdoc/>
    public async Task<PendingWorkRunResult> RunOperationAsync(string operation, CancellationToken cancellationToken = default)
    {
        if (!this._processors.TryGetValue(operation, out IPendingWorkProcessor? processor))
        {
            return new PendingWorkRunResult(0, 0, 0, 0);
        }

        DateTime nowUtc = this._timeProvider.GetUtcNow().UtcDateTime;

        IReadOnlyList<PendingWorkRequest> due = (await this._queue.GetPendingAsync(operation, cancellationToken))
            .Where(r => this.IsDue(r, nowUtc))
            .Take(this._options.MaxItemsPerRun)
            .ToList();

        if (due.Count == 0)
        {
            return new PendingWorkRunResult(0, 0, 0, 0);
        }

        foreach (PendingWorkRequest request in due)
        {
            await this._queue.MarkProcessingAsync(request.Id, cancellationToken);
        }

        IReadOnlyList<PendingWorkRequest> processed;
        int requestsSpent = 0;
        int daysSynced = 0;

        try
        {
            PendingWorkProcessingResult result = await processor.ProcessAsync(due, cancellationToken);
            processed = result.Processed;
            requestsSpent = result.RequestsSpent;
            daysSynced = result.DaysSynced;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Pending work processor {Operation} threw unexpectedly", operation);
            processed = Array.Empty<PendingWorkRequest>();
        }

        HashSet<Guid> processedIds = processed.Select(p => p.Id).ToHashSet();
        int processedCount = 0;
        int failedCount = 0;

        foreach (PendingWorkRequest request in due)
        {
            if (processedIds.Contains(request.Id))
            {
                await this._queue.MarkProcessedAsync(request.Id, cancellationToken);
                processedCount++;
            }
            else
            {
                await this._queue.MarkFailedAsync(
                    request.Id,
                    "Pending work processing failed; scheduling retry.",
                    this.RetryDelayFor(request),
                    nowUtc,
                    this._options.MaxAttempts,
                    cancellationToken);
                failedCount++;
            }
        }

        this._logger.LogInformation(
            "Pending work run for {Operation}: {Processed} processed, {Failed} failed, {RequestsSpent} requests",
            operation,
            processedCount,
            failedCount,
            requestsSpent);

        return new PendingWorkRunResult(processedCount, failedCount, requestsSpent, daysSynced);
    }

    /// <inheritdoc/>
    public async Task<PendingWorkRunResult> RunAllAsync(CancellationToken cancellationToken = default)
    {
        int processed = 0;
        int failed = 0;
        int requestsSpent = 0;
        int daysSynced = 0;

        foreach (string operation in this._processors.Keys)
        {
            PendingWorkRunResult result = await this.RunOperationAsync(operation, cancellationToken);
            processed += result.ProcessedItems;
            failed += result.FailedItems;
            requestsSpent += result.RequestsSpent;
            daysSynced += result.DaysSynced;
        }

        return new PendingWorkRunResult(processed, failed, requestsSpent, daysSynced);
    }

    private bool IsDue(PendingWorkRequest request, DateTime nowUtc)
    {
        return request.Status == PendingStatus.Pending
            || (request.Status == PendingStatus.Failed
                && request.NextRetryAtUtc != null
                && request.NextRetryAtUtc <= nowUtc);
    }

    private TimeSpan RetryDelayFor(PendingWorkRequest request)
    {
        double minutes = this._options.BaseRetryDelayMinutes * Math.Pow(2.0, request.Attempts);
        minutes = Math.Min(minutes, 24 * 60);
        return TimeSpan.FromMinutes(minutes);
    }
}
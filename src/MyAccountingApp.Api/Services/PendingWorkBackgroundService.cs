using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using Serilog;

namespace MyAccountingApp.Api.Services;

/// <summary>
/// Periodically processes the generic pending work queue in the background: runs one
/// dispatch pass at startup and then every configured interval, with exponential
/// backoff and a maximum number of attempts handled by the dispatcher.
/// </summary>
public sealed class PendingWorkBackgroundService : BackgroundService
{
    private readonly IPendingWorkDispatcher _dispatcher;
    private readonly PendingWorkOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingWorkBackgroundService"/> class.
    /// </summary>
    /// <param name="dispatcher">The pending work dispatcher.</param>
    /// <param name="options">Retry and batching options.</param>
    public PendingWorkBackgroundService(IPendingWorkDispatcher dispatcher, PendingWorkOptions options)
    {
        this._dispatcher = dispatcher;
        this._options = options;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PendingWorkRunResult result = await this._dispatcher.RunAllAsync(stoppingToken);

                if (result.ProcessedItems > 0 || result.FailedItems > 0)
                {
                    Log.Information(
                        "Pending work background run completed: {Processed} processed, {Failed} failed, {RequestsSpent} requests, {DaysSynced} days synced",
                        result.ProcessedItems,
                        result.FailedItems,
                        result.RequestsSpent,
                        result.DaysSynced);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Pending work background run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(this._options.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
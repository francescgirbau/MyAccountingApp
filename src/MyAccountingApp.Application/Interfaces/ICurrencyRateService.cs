using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Defines a service for retrieving currency conversion rates for a specific date.
/// </summary>
public interface ICurrencyRateService
{
    /// <summary>
    /// Asynchronously retrieves conversion rates for all supported currencies based on the specified date.
    /// </summary>
    /// <param name="date">The date for which to retrieve currency conversion rates.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary
    /// mapping currencies to their conversion rates.
    /// </returns>
    Task<Dictionary<Currencies, decimal>> GetQuotes(DateTime date);

    /// <summary>
    /// Asynchronously retrieves the conversion for the specified date, fetching from the API when
    /// the date is missing and quota allows, and falling back to a stale conversion otherwise.
    /// </summary>
    /// <param name="date">The date for which to retrieve the conversion.</param>
    /// <returns>The conversion for the requested date, which may be marked as stale.</returns>
    Task<Conversion> GetConversionAsync(DateTime date);

    /// <summary>
    /// Asynchronously retrieves one quote per currency for the specified date, exposing both the
    /// requested date and the actual rate date so stale fallbacks remain traceable.
    /// </summary>
    /// <param name="date">The date for which to retrieve the quotes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The quotes for the requested date, each marked stale when a fallback was used.</returns>
    Task<IReadOnlyList<FxQuoteDto>> GetFxQuotesAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches and persists conversions for a range of dates using a single timeseries request.
    /// </summary>
    /// <param name="start">The first date of the range (inclusive).</param>
    /// <param name="end">The last date of the range (inclusive).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the range was fetched; false if there was no quota available.</returns>
    Task<bool> SyncRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the pending conversion queue, grouping dates into timeseries ranges.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A summary of the processing run.</returns>
    Task<PendingProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Backfills conversions from the start of the current period if the repository is empty.
    /// </summary>
    /// <param name="days">The number of days to backfill.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the backfill was performed; otherwise, false.</returns>
    Task<bool> BackfillIfEmptyAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches and persists conversions for any gap between the last cached day and yesterday,
    /// chunked into timeseries ranges of the configured maximum size.
    /// </summary>
    /// <param name="maxDays">The maximum number of days a single timeseries request may cover.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of new conversion days persisted.</returns>
    Task<int> SyncGapAsync(int maxDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current conversion store status.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current status.</returns>
    Task<ConversionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current currency API quota.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current quota.</returns>
    Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default);
}

using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Provides currency rate retrieval with a cache-first, quota-aware orchestration.
/// Uses a repository for local storage, a quota manager to respect API limits, and
/// a queue for dates that could not be fetched immediately.
/// </summary>
public class CurencyRateService : ICurrencyRateService
{
    private readonly IConversionRepository _repository;
    private readonly ICurrencyConverter _api;
    private readonly Currencies _source;
    private readonly IApiQuotaManager _quotaManager;
    private readonly IPendingConversionQueue _pendingQueue;
    private readonly int _maxTimeseriesDays;
    private readonly string _sourceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurencyRateService"/> class.
    /// </summary>
    /// <param name="repository">Repository for storing currency conversions.</param>
    /// <param name="api">External API for fetching currency rates.</param>
    /// <param name="source">Base currency for conversion.</param>
    /// <param name="quotaManager">Manager for the API request quota.</param>
    /// <param name="pendingQueue">Queue for dates that could not be fetched immediately.</param>
    /// <param name="maxTimeseriesDays">Maximum number of days a single timeseries request may cover.</param>
    /// <param name="sourceProvider">Name of the provider that supplies the rates.</param>
    /// <exception cref="ArgumentException">Thrown if the source currency is not EUR.</exception>
    public CurencyRateService(
        IConversionRepository repository,
        ICurrencyConverter api,
        Currencies source,
        IApiQuotaManager quotaManager,
        IPendingConversionQueue pendingQueue,
        int maxTimeseriesDays = 365,
        string sourceProvider = "frankfurter")
    {
        this._repository = repository;
        this._api = api;
        this._source = source;
        this._quotaManager = quotaManager;
        this._pendingQueue = pendingQueue;
        this._maxTimeseriesDays = maxTimeseriesDays;
        this._sourceProvider = sourceProvider;
        this.Validate();
    }

    /// <summary>
    /// Validates the base currency. Currently only EUR is supported.
    /// ToDo, remove limitation to support non EUR as base currency.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the base currency is not EUR.</exception>
    private void Validate()
    {
        string parentType = nameof(CurencyRateService);

        if (this._source != Currencies.EUR)
        {
            string message = $"The {nameof(this._source)} must be {Currencies.EUR}, you provided {this._source}";
            throw new ArgumentException(message, parentType);
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<Currencies, decimal>> GetQuotes(DateTime date)
    {
        Conversion conversion = await this.GetConversionAsync(date);
        return conversion.Quotes;
    }

    /// <inheritdoc/>
    public async Task<Conversion> GetConversionAsync(DateTime date)
    {
        Conversion? existing = this._repository.GetByDate(date);

        if (existing != null)
        {
            return existing;
        }

        DateOnly day = DateOnly.FromDateTime(date.Date);

        await this._quotaManager.EnsurePeriodAsync();

        if (await this._quotaManager.TryConsumeAsync(1))
        {
            try
            {
                Dictionary<string, decimal> rates = await this._api.FetchAllRatesAsync(this._source, date);
                Conversion conversion = this.BuildConversion(day, rates);
                this._repository.AddOrUpdate(conversion);
                return conversion;
            }
            catch (CurrencyApiQuotaExceededException)
            {
                await this._quotaManager.MarkExhaustedAsync();
            }
        }

        await this._pendingQueue.EnqueueAsync(day);

        Conversion? fallback = this.FindFallback(day);

        if (fallback != null)
        {
            return CloneForStale(fallback);
        }

        throw new ConversionNotAvailableException($"No conversion available for {day:yyyy-MM-dd} and no API quota to fetch it.");
    }

    /// <inheritdoc/>
    public async Task<bool> SyncRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        await this._quotaManager.EnsurePeriodAsync(cancellationToken);

        if (!await this._quotaManager.TryConsumeAsync(1, cancellationToken))
        {
            return false;
        }

        try
        {
            IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> rates = await this._api.FetchRangeAsync(this._source, start, end, null, cancellationToken);

            foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> kv in rates)
            {
                this._repository.AddOrUpdate(this.BuildConversion(kv.Key, kv.Value));
            }

            return true;
        }
        catch (CurrencyApiQuotaExceededException)
        {
            await this._quotaManager.MarkExhaustedAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<PendingProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        await this._quotaManager.EnsurePeriodAsync(cancellationToken);

        IReadOnlyList<PendingConversionRequest> pending = await this._pendingQueue.GetPendingAsync(cancellationToken);
        List<DateOnly> pendingDays = pending.Select(p => p.Date).Distinct().OrderBy(d => d).ToList();

        int processedDays = 0;
        int requestsSpent = 0;
        int failures = 0;

        foreach ((DateOnly start, DateOnly end) in GroupIntoRanges(pendingDays, this._maxTimeseriesDays))
        {
            if (!await this._quotaManager.TryConsumeAsync(1, cancellationToken))
            {
                break;
            }

            requestsSpent++;

            try
            {
                IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> rates = await this._api.FetchRangeAsync(this._source, start, end, null, cancellationToken);

                foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> kv in rates)
                {
                    this._repository.AddOrUpdate(this.BuildConversion(kv.Key, kv.Value));
                    await this._pendingQueue.MarkProcessedAsync(kv.Key, cancellationToken);
                    processedDays++;
                }
            }
            catch (CurrencyApiQuotaExceededException)
            {
                await this._quotaManager.MarkExhaustedAsync(cancellationToken);
                break;
            }
            catch
            {
                foreach (DateOnly day in pendingDays.Where(d => d >= start && d <= end))
                {
                    await this._pendingQueue.MarkFailedAsync(day, "Range fetch failed.", cancellationToken);
                }

                failures++;
            }
        }

        return new PendingProcessingResult(processedDays, requestsSpent, failures);
    }

    /// <inheritdoc/>
    public async Task<bool> BackfillIfEmptyAsync(int days, CancellationToken cancellationToken = default)
    {
        if (this._repository.GetAll().Any())
        {
            return false;
        }

        DateOnly end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly start = end.AddDays(-(days - 1));
        return await this.SyncRangeAsync(start, end, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        return this._quotaManager.GetQuotaAsync(cancellationToken);
    }

    private Conversion BuildConversion(DateOnly day, Dictionary<string, decimal> rates)
    {
        Conversion conversion = new(day.ToDateTime(TimeOnly.MinValue), this._source, sourceProvider: this._sourceProvider);
        int prefixLength = this._source.ToString().Length;

        foreach (KeyValuePair<string, decimal> kv in rates)
        {
            string targetCode = kv.Key.Substring(prefixLength);

            if (Enum.TryParse<Currencies>(targetCode, out Currencies currency))
            {
                conversion.AddOrUpdateQuote(currency, kv.Value);
            }
        }

        conversion.MarkFresh(DateTime.UtcNow);
        return conversion;
    }

    private Conversion? FindFallback(DateOnly day)
    {
        DateTime date = day.ToDateTime(TimeOnly.MinValue);
        return this._repository.GetAll()
            .OrderByDescending(c => c.Date)
            .FirstOrDefault(c => c.Date.Date <= date);
    }

    private static Conversion CloneForStale(Conversion fallback)
    {
        Conversion clone = new(
            fallback.Date,
            fallback.Source,
            new Dictionary<Currencies, decimal>(fallback.Quotes),
            fallback.RetrievedAtUtc,
            fallback.IsStale,
            fallback.SourceProvider);
        clone.MarkStale();
        return clone;
    }

    private static List<(DateOnly Start, DateOnly End)> GroupIntoRanges(List<DateOnly> dates, int maxDays)
    {
        List<(DateOnly, DateOnly)> ranges = new();

        if (dates.Count == 0)
        {
            return ranges;
        }

        DateOnly rangeStart = dates[0];
        DateOnly rangeEnd = dates[0];

        for (int i = 1; i < dates.Count; i++)
        {
            DateOnly day = dates[i];
            bool consecutive = day.AddDays(-1) <= rangeEnd;
            bool fits = day.DayNumber - rangeStart.DayNumber < maxDays;

            if (consecutive && fits)
            {
                rangeEnd = day;
            }
            else
            {
                ranges.Add((rangeStart, rangeEnd));
                rangeStart = day;
                rangeEnd = day;
            }
        }

        ranges.Add((rangeStart, rangeEnd));
        return ranges;
    }
}

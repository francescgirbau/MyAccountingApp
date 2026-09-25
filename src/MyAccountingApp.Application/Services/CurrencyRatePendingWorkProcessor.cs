using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Processes <c>CurrencyRate</c> pending work items: groups the queued dates into
/// contiguous ranges, fetches the rates when API quota allows and persists the results.
/// Requests whose date was persisted in a successful fetch are returned as processed;
/// everything else is left for the queue to retry with backoff.
/// </summary>
public sealed class CurrencyRatePendingWorkProcessor : IPendingWorkProcessor
{
    private readonly IConversionRepository _repository;
    private readonly ICurrencyConverter _api;
    private readonly IApiQuotaManager _quotaManager;
    private readonly Currencies _source;
    private readonly string _sourceProvider;
    private readonly int _maxTimeseriesDays;
    private readonly ILogger<CurrencyRatePendingWorkProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyRatePendingWorkProcessor"/> class.
    /// </summary>
    /// <param name="repository">Repository for storing currency conversions.</param>
    /// <param name="api">External API for fetching currency rates.</param>
    /// <param name="quotaManager">Manager for the API request quota.</param>
    /// <param name="source">Base currency for conversion.</param>
    /// <param name="sourceProvider">Name of the provider that supplies the rates.</param>
    /// <param name="maxTimeseriesDays">Maximum number of days a single timeseries request may cover.</param>
    /// <param name="logger">Logger for structured observability of the pending processing.</param>
    public CurrencyRatePendingWorkProcessor(
        IConversionRepository repository,
        ICurrencyConverter api,
        IApiQuotaManager quotaManager,
        Currencies source,
        string sourceProvider,
        int maxTimeseriesDays = 365,
        ILogger<CurrencyRatePendingWorkProcessor>? logger = null)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this._api = api ?? throw new ArgumentNullException(nameof(api));
        this._quotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
        this._source = source;
        this._sourceProvider = sourceProvider;
        this._maxTimeseriesDays = maxTimeseriesDays;
        this._logger = logger ?? NullLogger<CurrencyRatePendingWorkProcessor>.Instance;
    }

    /// <inheritdoc/>
    public string Operation => PendingWorkOperations.CurrencyRate;

    /// <inheritdoc/>
    public async Task<PendingWorkProcessingResult> ProcessAsync(
        IReadOnlyList<PendingWorkRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return new PendingWorkProcessingResult(Array.Empty<PendingWorkRequest>(), 0, 0);
        }

        await this._quotaManager.EnsurePeriodAsync(cancellationToken);

        List<DateOnly> days = requests
            .Select(CurrencyRatePendingWorkPayload.ReadDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        List<PendingWorkRequest> processed = new();
        int requestsSpent = 0;
        int daysSynced = 0;

        foreach ((DateOnly start, DateOnly end) in DateRangeGrouper.GroupIntoRanges(days, this._maxTimeseriesDays))
        {
            if (!await this.CanConsumeAsync(cancellationToken))
            {
                break;
            }

            try
            {
                IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> rates = await this._api.FetchRangeAsync(this._source, start, end, null, cancellationToken);
                await this._quotaManager.TryConsumeAsync(1, cancellationToken);
                requestsSpent++;

                foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> kv in rates)
                {
                    this._repository.AddOrUpdate(this.BuildConversion(kv.Key, kv.Value));
                    daysSynced++;
                }

                processed.AddRange(requests.Where(r => rates.ContainsKey(CurrencyRatePendingWorkPayload.ReadDate(r))));
            }
            catch (CurrencyApiQuotaExceededException)
            {
                this._logger.LogWarning("Currency API quota exhausted for {Provider}", this._sourceProvider);
                await this._quotaManager.MarkExhaustedAsync(cancellationToken);
                break;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to fetch pending range {Start}..{End}", start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
            }
        }

        this._logger.LogInformation(
            "Processed pending currency requests: {Processed} items, {RequestsSpent} requests, {DaysSynced} days",
            processed.Count,
            requestsSpent,
            daysSynced);

        return new PendingWorkProcessingResult(processed, requestsSpent, daysSynced);
    }

    private async Task<bool> CanConsumeAsync(CancellationToken cancellationToken)
    {
        ApiUsageQuota quota = await this._quotaManager.GetQuotaAsync(cancellationToken);
        return quota.CanConsume(1);
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
}
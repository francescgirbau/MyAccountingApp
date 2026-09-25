using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Http.Currency;

/// <summary>
/// Resolves currency conversions across multiple providers: a default provider serves
/// fiat pairs while dedicated providers serve specific currencies (e.g. BTC via
/// CoinGecko). Keeps every pair on its own source without polluting the fiat series.
/// </summary>
public class CompositeCurrencyConverter : ICurrencyConverter
{
    private readonly ICurrencyConverter _defaultProvider;
    private readonly IReadOnlyDictionary<Currencies, ICurrencyConverter> _dedicatedProviders;
    private readonly IReadOnlyCollection<string> _excludedCurrencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCurrencyConverter"/> class.
    /// </summary>
    /// <param name="defaultProvider">The provider that serves the remaining (fiat) currencies.</param>
    /// <param name="dedicatedProviders">Optional map of currency to its dedicated provider.</param>
    /// <param name="excludedCurrencies">Optional list of currency codes to skip entirely.</param>
    public CompositeCurrencyConverter(
        ICurrencyConverter defaultProvider,
        IReadOnlyDictionary<Currencies, ICurrencyConverter>? dedicatedProviders = null,
        IReadOnlyCollection<string>? excludedCurrencies = null)
    {
        ArgumentNullException.ThrowIfNull(defaultProvider);

        this._defaultProvider = defaultProvider;
        this._dedicatedProviders = dedicatedProviders ?? new Dictionary<Currencies, ICurrencyConverter>();
        this._excludedCurrencies = excludedCurrencies ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        Dictionary<string, decimal> result = new(await this._defaultProvider.FetchAllRatesAsync(source, date), StringComparer.Ordinal);

        foreach (KeyValuePair<Currencies, ICurrencyConverter> entry in this._dedicatedProviders)
        {
            if (!this.IsRequested(entry.Key))
            {
                continue;
            }

            Dictionary<string, decimal> dedicated = await entry.Value.FetchAllRatesAsync(source, date);

            foreach (KeyValuePair<string, decimal> pair in dedicated)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
        Currencies source,
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<Currencies>? targets = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Currencies> defaultTargets = this.ResolveDefaultTargets(targets);
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> merged = new Dictionary<DateOnly, Dictionary<string, decimal>>();

        if (defaultTargets.Count > 0 || targets == null)
        {
            merged = await this._defaultProvider.FetchRangeAsync(source, start, end, defaultTargets.Count > 0 ? defaultTargets : null, cancellationToken);
        }

        Dictionary<DateOnly, Dictionary<string, decimal>> result = new();

        foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> day in merged)
        {
            result[day.Key] = new Dictionary<string, decimal>(day.Value, StringComparer.Ordinal);
        }

        foreach (KeyValuePair<Currencies, ICurrencyConverter> entry in this._dedicatedProviders)
        {
            if (!this.IsRequested(entry.Key) || (targets != null && !targets.Contains(entry.Key)))
            {
                continue;
            }

            IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> dedicated = await entry.Value.FetchRangeAsync(
                source, start, end, new[] { entry.Key }, cancellationToken);

            foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> day in dedicated)
            {
                if (!result.TryGetValue(day.Key, out Dictionary<string, decimal>? quotes))
                {
                    quotes = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    result[day.Key] = quotes;
                }

                foreach (KeyValuePair<string, decimal> pair in day.Value)
                {
                    quotes[pair.Key] = pair.Value;
                }
            }
        }

        return result;
    }

    private IReadOnlyCollection<Currencies> ResolveDefaultTargets(IReadOnlyCollection<Currencies>? targets)
    {
        if (targets == null)
        {
            return Enum.GetValues<Currencies>().Where(c => !this._dedicatedProviders.ContainsKey(c)).ToList();
        }

        return targets.Where(c => !this._dedicatedProviders.ContainsKey(c)).ToList();
    }

    private bool IsRequested(Currencies currency)
    {
        return !this._excludedCurrencies.Contains(currency.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
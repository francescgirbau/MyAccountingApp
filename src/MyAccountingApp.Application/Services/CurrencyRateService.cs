using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Provides currency rate retrieval and caching functionality.
/// Uses a repository for local storage and an external API for fetching rates.
/// </summary>
public class CurrencyRateService : ICurrencyRateService
{
    private readonly IConversionRepository _repository;
    private readonly ICurrencyConverter _api;
    private readonly Currencies _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyRateService"/> class.
    /// </summary>
    /// <param name="repository">Repository for storing currency conversions.</param>
    /// <param name="api">External API for fetching currency rates.</param>
    /// <param name="source">Base currency for conversion.</param>
    /// <exception cref="ArgumentException">Thrown if the source currency is not EUR.</exception>
    public CurrencyRateService(IConversionRepository repository, ICurrencyConverter api, Currencies source)
    {
        this._repository = repository;
        this._api = api;
        this._source = source;
        this.Validate();
    }

    /// <summary>
    /// Validates the base currency. Currently only EUR is supported.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the base currency is not EUR.</exception>
    private void Validate()
    {
        string parentType = nameof(CurrencyRateService);

        if (this._source != Currencies.EUR) // ToDo, remove limitation to support non EUR as base currency
        {
            string message = $"The {nameof(this._source)} must be {Currencies.EUR}, you provided {this._source}";
            throw new ArgumentException(message, parentType);
        }
    }

    /// <summary>
    /// Gets currency conversion quotes for a specific date.
    /// If not cached, fetches from the external API and stores the result.
    /// </summary>
    /// <param name="date">The date for which to retrieve currency quotes.</param>
    /// <returns>
    /// A dictionary mapping target currencies to their conversion rates.
    /// </returns>
    public async Task<Dictionary<Currencies, double>> GetQuotes(DateTime date)
    {
        Conversion? conversion = this._repository.GetByDate(date);

        if (conversion != null)
        {
            return conversion.Quotes;
        }

        Dictionary<string, double> rates = await this._api.FetchAllRatesAsync(this._source, date);

        conversion = new Conversion(date, this._source);

        foreach (KeyValuePair<string, double> kv in rates)
        {
            string targetCurrencyCode = kv.Key.Substring(3);

            if (Enum.TryParse<Currencies>(targetCurrencyCode, out Currencies currency))
            {
                conversion.AddOrUpdateQuote(currency, kv.Value);
            }
        }

        this._repository.Add(conversion);

        return conversion.Quotes;
    }
}

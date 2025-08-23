using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Application.Services;

public class CurrencyRateService : ICurrencyRateService
{
    private readonly IConversionRepository _repository;
    private readonly ICurrencyConverter _api;
    private readonly Currencies _source;

    private static readonly Currencies[] SupportedCurrencies = Enum.GetValues<Currencies>();

    public CurrencyRateService(IConversionRepository repository, ICurrencyConverter api, Currencies source)
    {
        _repository = repository;
        _api = api;
        _source = source;
        Validate();

    }

    private void Validate()
    {
        string parentType = nameof(CurrencyRateService);
        //ToDo, remove limitation to support non EUR as base currency
        if (_source != Currencies.EUR)
        {
            string message = $"The {nameof(_source)} must be {Currencies.EUR}, you provided {_source}";
            throw new ArgumentException(message, parentType);
        }
    }

    public async Task<Dictionary<Currencies, double>> GetQuotes(DateTime date)
    {

        Conversion? conversion = _repository.GetByDate(date);

        if (conversion != null)
        {
            return conversion.Quotes;
        }

        Dictionary<string, double> rates = await _api.FetchAllRatesAsync(_source, date);

        conversion = new Conversion(date, _source);

        foreach (KeyValuePair<string, double> kv in rates)
        {
            string targetCurrencyCode = kv.Key.Substring(3);

            if (Enum.TryParse<Currencies>(targetCurrencyCode, out Currencies currency))
            {
                conversion.AddOrUpdateQuote(currency, kv.Value);
            }
        }

        _repository.Add(conversion);


        return conversion.Quotes;
    }

}

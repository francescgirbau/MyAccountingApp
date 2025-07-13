using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Infrastructure.Services;

namespace MyAccountingApp.Application.Services;

public class CurrencyConversionService
{
    private readonly IConversionRepository _repository;
    private readonly CurrencyConverter _api;

    private static readonly Currencies[] SupportedCurrencies = Enum.GetValues<Currencies>();

    public CurrencyConversionService(IConversionRepository repository, CurrencyConverter api)
    {
        _repository = repository;
        _api = api;
    }

    public async Task<double> GetExchangeRateAsync(Currencies targetCurrency, DateTime date)
    {
        if (targetCurrency == Currencies.EUR)
            return 1.0;

        Conversion? conversion = _repository.GetByDate(date);

        if (conversion == null)
        {
            var rates = await _api.FetchAllRatesAsync(Currencies.EUR, date);

            conversion = new Conversion(date, Currencies.EUR);

            foreach (var kv in rates)
            {
                string targetCurrencyCode = kv.Key.Substring(3);

                if (Enum.TryParse<Currencies>(targetCurrencyCode, out var currency))
                {
                    conversion.AddOrUpdateQuote(currency, kv.Value);
                }
            }

            _repository.Add(conversion);
        }

        if (!conversion.TryGetQuote(targetCurrency, out double rate))
            throw new InvalidOperationException($"No s'ha trobat el canvi per {targetCurrency} el {date:yyyy-MM-dd}");

        return rate;
    }
}

using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeCurrencyConverter : ICurrencyConverter
{
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        await Task.Delay(1); // simulate async
        return new Dictionary<string, decimal>
        {
            { "EURUSD", 1.1m },
            { "EURCAD", 1.5m },
        };
    }
}

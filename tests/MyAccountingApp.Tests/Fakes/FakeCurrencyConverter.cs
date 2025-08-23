using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
namespace MyAccountingApp.Tests.Fakes;

public class FakeCurrencyConverter : ICurrencyConverter
{
    public async Task<Dictionary<string, double>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        await Task.Delay(1); // simulate async
        return new Dictionary<string, double>
        {
            { "EURUSD", 1.1 },
            { "EURCAD", 1.5 }
        };
    }
}

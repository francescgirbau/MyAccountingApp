using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Core.Interfaces;

public interface ICurrencyConverter
{
    Task<Dictionary<string, double>> FetchAllRatesAsync(Currencies source, DateTime date);
}

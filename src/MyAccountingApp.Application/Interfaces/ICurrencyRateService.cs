using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Application.Interfaces;

public interface ICurrencyRateService
{
    Task<Dictionary<Currencies, double>> GetQuotes(DateTime date);
}

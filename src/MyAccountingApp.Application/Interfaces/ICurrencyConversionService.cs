using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.ValueObjects;

namespace MyAccountingApp.Application.Interfaces;

public interface ICurrencyConversionService
{
    Task<Money> ConvertToAsync(Money original, Currencies targetCurrency, DateTime date);
}
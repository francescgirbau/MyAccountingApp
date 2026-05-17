using FsCheck.Xunit;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Tests.Properties;

public class ConversionPropertyTests
{
    [Property]
    public bool TryGetQuote_ReturnsStoredRate(decimal rate)
    {
        rate = Math.Abs(rate);
        if (rate == 0)
        {
            rate = 1.0m;
        }

        Conversion conversion = new Conversion(new DateTime(2024, 1, 1), Currencies.EUR);
        conversion.AddOrUpdateQuote(Currencies.USD, rate);

        bool found = conversion.TryGetQuote(Currencies.USD, out decimal storedRate);

        return found && storedRate == rate;
    }

    [Property]
    public bool AddOrUpdateQuote_ReplacesExistingRate(decimal firstRate, decimal secondRate)
    {
        firstRate = Math.Abs(firstRate);
        secondRate = Math.Abs(secondRate);
        if (firstRate == 0)
        {
            firstRate = 1.0m;
        }

        if (secondRate == 0)
        {
            secondRate = 2.0m;
        }

        Conversion conversion = new Conversion(new DateTime(2024, 1, 1), Currencies.EUR);

        conversion.AddOrUpdateQuote(Currencies.USD, firstRate);
        conversion.AddOrUpdateQuote(Currencies.USD, secondRate);

        conversion.TryGetQuote(Currencies.USD, out decimal stored);

        return stored == secondRate;
    }

    [Property]
    public bool TryGetQuote_ForDifferentCurrency_ReturnsFalse()
    {
        Conversion conversion = new Conversion(new DateTime(2024, 1, 1), Currencies.EUR);
        conversion.AddOrUpdateQuote(Currencies.USD, 1.1m);

        bool found = conversion.TryGetQuote(Currencies.CAD, out _);

        return !found;
    }

    [Property]
    public bool MatchesDate_MatchesExactDate(int year, int month, int day)
    {
        month = Math.Clamp(month, 1, 12);
        day = Math.Clamp(day, 1, 28);
        year = Math.Clamp(year, 2000, 2030);

        DateTime date = new DateTime(year, month, day);
        Conversion conversion = new Conversion(date, Currencies.EUR);

        return conversion.MatchesDate(date);
    }
}

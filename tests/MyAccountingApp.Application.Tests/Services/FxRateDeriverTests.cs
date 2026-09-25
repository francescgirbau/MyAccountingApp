using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;

namespace MyAccountingApp.Application.Tests.Services;

public class FxRateDeriverTests
{
    private static readonly Dictionary<Currencies, decimal> EurQuotes = new()
    {
        { Currencies.USD, 1.07m },
        { Currencies.GBP, 0.85m },
        { Currencies.BTC, 0.00001666m },
    };

    [Fact]
    public void Derive_FromEur_ReturnsStoredQuote()
    {
        // Act
        decimal rate = FxRateDeriver.Derive(Currencies.EUR, Currencies.USD, EurQuotes);

        // Assert
        Assert.Equal(1.07m, rate);
    }

    [Fact]
    public void Derive_ToEur_ReturnsInverseOfStoredQuote()
    {
        // Act
        decimal rate = FxRateDeriver.Derive(Currencies.USD, Currencies.EUR, EurQuotes);

        // Assert
        Assert.Equal(1m / 1.07m, rate);
    }

    [Fact]
    public void Derive_CrossRate_DividesStoredQuotes()
    {
        // Act
        decimal rate = FxRateDeriver.Derive(Currencies.USD, Currencies.GBP, EurQuotes);

        // Assert
        Assert.Equal(0.85m / 1.07m, rate);
    }

    [Fact]
    public void Derive_Identity_ReturnsOne()
    {
        // Act
        decimal rate = FxRateDeriver.Derive(Currencies.USD, Currencies.USD, EurQuotes);

        // Assert
        Assert.Equal(1m, rate);
    }

    [Fact]
    public void Derive_BtcAgainstEur_InvertsBtcQuote()
    {
        // Act
        decimal eurPerBtc = FxRateDeriver.Derive(Currencies.BTC, Currencies.EUR, EurQuotes);
        decimal usdPerBtc = FxRateDeriver.Derive(Currencies.BTC, Currencies.USD, EurQuotes);

        // Assert
        Assert.Equal(1m / 0.00001666m, eurPerBtc);
        Assert.Equal(1.07m / 0.00001666m, usdPerBtc);
    }

    [Fact]
    public void Derive_ThrowsConversionNotAvailable_WhenQuoteMissing()
    {
        // Act & Assert
        Assert.Throws<ConversionNotAvailableException>(() => FxRateDeriver.Derive(Currencies.USD, Currencies.JPY, EurQuotes));
        Assert.Throws<ConversionNotAvailableException>(() => FxRateDeriver.Derive(Currencies.JPY, Currencies.USD, EurQuotes));
    }

    [Fact]
    public void DeriveAll_ReturnsEveryQuoteExceptTheBase()
    {
        // Act
        IReadOnlyDictionary<Currencies, decimal> derived = FxRateDeriver.DeriveAll(Currencies.USD, EurQuotes);

        // Assert
        Assert.Equal(2, derived.Count);
        Assert.False(derived.ContainsKey(Currencies.USD));
        Assert.Equal(0.85m / 1.07m, derived[Currencies.GBP]);
        Assert.Equal(0.00001666m / 1.07m, derived[Currencies.BTC]);
    }
}
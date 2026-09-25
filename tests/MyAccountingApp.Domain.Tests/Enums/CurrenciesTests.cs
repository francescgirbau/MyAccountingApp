using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Tests.Enums;

public class CurrenciesTests
{
    [Theory]
    [InlineData("PLN")]
    [InlineData("DKK")]
    [InlineData("CZK")]
    [InlineData("HUF")]
    [InlineData("NZD")]
    [InlineData("KRW")]
    [InlineData("THB")]
    [InlineData("IDR")]
    [InlineData("MYR")]
    [InlineData("PHP")]
    [InlineData("RON")]
    [InlineData("ISK")]
    public void Parse_AllFrankfurterCurrencies_AreSupported(string code)
    {
        // Act
        bool parsed = Enum.TryParse<Currencies>(code, out Currencies currency);

        // Assert
        Assert.True(parsed);
        Assert.Equal(code, currency.ToString());
    }
}
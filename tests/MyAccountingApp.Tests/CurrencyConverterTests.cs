using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.ValueObjects;
using MyAccountingApp.Infrastructure;
using MyAccountingApp.Infrastructure.Services;
using Xunit;

namespace MyAccountingApp.Tests
{
    public class CurrencyConverterTests
    {
        [Fact]
        public async Task ConvertToAsync_ShouldConvert_USD_To_EUR_OnGivenDate()
        {
            // Arrange
            CurrencyConverter converter = new CurrencyConverter();
            Money money = new Money { Amount = 100, Currency = Currencies.USD };
            DateTime date = new DateTime(2023, 12, 1);

            // Act
            var result = await converter.ConvertToAsync(money, Currencies.EUR, date);

            // Assert
            Assert.Equal(Currencies.EUR, result.Currency);
            Assert.True(result.Amount > 0 && result.Amount < 200); // marge ample per evitar fallades per canvi de quotes
        }
    }
}

using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Core.ValueObjects;
using MyAccountingApp.Infrastructure;
using MyAccountingApp.Infrastructure.Services;
using MyAccountingApp.Tests.Fakes;
using Xunit;

namespace MyAccountingApp.Tests
{
    public class CurrencyConverterTests
    {
        [Fact]
        public async Task ConvertToAsync_ShouldConvert_USD_To_EUR_OnGivenDate()
        {
            // Arrange
            ICurrencyConverter converter = new FakeCurrencyConverter();
            Currencies source = Currencies.EUR;
            (string, double) expectedRateUsd = ("EURUSD", 1.1 );
            (string, double) expectedRateCad = ("EURCAD", 1.5);


            DateTime date = new DateTime(2023, 12, 1);

            // Act
            Dictionary<string, double> result = await converter.FetchAllRatesAsync(source, date);

            // Assert
            Assert.True(result.ContainsKey(expectedRateUsd.Item1));
            Assert.Equal(result[expectedRateUsd.Item1], expectedRateUsd.Item2);
            Assert.True(result.ContainsKey(expectedRateCad.Item1));
            Assert.Equal(result[expectedRateCad.Item1], expectedRateCad.Item2);
        }
    }
}

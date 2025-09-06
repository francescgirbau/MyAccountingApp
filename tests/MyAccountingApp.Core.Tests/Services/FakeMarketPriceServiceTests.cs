using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Core.Tests.Services;
public class FakeMarketPriceServiceTests
{
    [Fact]
    public async Task Can_GetPrice_FromFakeService()
    {
        // Arrange
        FakeMarketPriceService fakeService = new FakeMarketPriceService();

        // Act
        Money? price = await fakeService.GetPriceAsync("AAPL");

        // Assert
        Assert.NotNull(price);
        Assert.Equal(150.25, price!.Amount);
        Assert.Equal("USD", price.Currency);
    }

    [Fact]
    public async Task ReturnsNull_ForUnknownSymbol()
    {
        // Arrange
        FakeMarketPriceService fakeService = new FakeMarketPriceService();

        // Act
        Money? price = await fakeService.GetPriceAsync("UNKNOWN");

        // Assert
        Assert.Null(price);
    }
}

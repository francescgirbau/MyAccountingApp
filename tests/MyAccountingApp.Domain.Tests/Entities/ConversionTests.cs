using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Tests.Entities;

public class ConversionTests
{
    [Fact]
    public void Constructor_WithNonEURSource_ThrowsArgumentException()
    {
        // Arrange
        Currencies invalidSource = Currencies.USD;
        DateTime date = new DateTime(2023, 12, 1);

        // Act
        Action action = () => { new Conversion(date, invalidSource); };

        // Assert
        Assert.Throws<ArgumentException>(() => action());
    }

    [Fact]
    public void AddOrUpdateQuote_WithSourceAsTarget_ThrowsInvalidOperationException()
    {
        // Arrange
        Conversion conversion = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        Action action = () => conversion.AddOrUpdateQuote(Currencies.EUR, 1.0);

        // Assert
        Assert.Throws<InvalidOperationException>(() => action());
    }

    [Fact]
    public void TryGetQuote_ReturnsCorrectValue()
    {
        // Arrange
        Conversion conversion = new Conversion(DateTime.Today, Currencies.EUR);
        conversion.AddOrUpdateQuote(Currencies.USD, 1.2);

        // Act
        bool exists = conversion.TryGetQuote(Currencies.USD, out double rate);

        // Assert
        Assert.True(exists);
        Assert.Equal(1.2, rate);
    }

    [Fact]
    public void TryGetQuote_ReturnsNonValue_WhenNoQuotes()
    {
        // Arrange
        Conversion conversion = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        bool exists = conversion.TryGetQuote(Currencies.USD, out double rate);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void MatchesDate_ReturnsTrue_WhenSameDate()
    {
        // Arrange
        DateTime date = new DateTime(2023, 12, 1);
        Conversion conversion = new Conversion(date, Currencies.EUR);

        // Act
        bool result = conversion.MatchesDate(date);

        // Assert
        Assert.True(result);
    }
}

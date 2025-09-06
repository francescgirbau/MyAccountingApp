using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Tests.Domain.ValueObjects
{
    public class MoneyTests
    {
        [Fact]
        public void Money_WithSameCurrencyAndAmount_ShouldBeEqual()
        {
            // Arrange
            Money m1 = new Money(100, "EUR");
            Money m2 = new Money(100, "EUR");

            // Act & Assert
            Assert.Equal(m1, m2);
        }

        [Fact]
        public void Money_WithSameCurrencyDifferentCase_ShouldBeEqual()
        {
            // Arrange
            Money m1 = new Money(100, "eur");
            Money m2 = new Money(100, "EUR");

            // Act & Assert
            Assert.Equal(m1, m2);
        }

        [Fact]
        public void Money_WithDifferentCurrency_ShouldNotBeEqual()
        {
            // Arrange
            Money m1 = new Money(100, "EUR");
            Money m2 = new Money(100, "USD");

            // Act & Assert
            Assert.NotEqual(m1, m2);
        }

        [Fact]
        public void Money_WithDifferentAmount_ShouldNotBeEqual()
        {
            // Arrange
            Money m1 = new Money(100, "EUR");
            Money m2 = new Money(200, "EUR");

            // Act & Assert
            Assert.NotEqual(m1, m2);
        }

        [Fact]
        public void IsSameCurrency_ShouldReturnTrue_IfCurrencyIsSame()
        {
            // Arrange
            Money m1 = new Money(50, "EUR");
            Money m2 = new Money(20, "EUR");

            // Act & Assert
            Assert.Equal(m1.Currency, m2.Currency);
        }

        [Fact]
        public void IsSameCurrency_ShouldBeCaseInsensitive()
        {
            // Arrange
            Money m1 = new Money(50, "eur");
            Money m2 = new Money(20, "EUR");

            // Act & Assert
            Assert.Equal(m1.Currency, m2.Currency);
        }

        [Fact]
        public void IsSameCurrency_ShouldReturnFalse_IfDifferentCurrency()
        {
            // Arrange
            Money m1 = new Money(50, "EUR");
            Money m2 = new Money(20, "USD");

            // Act & Assert
            Assert.NotEqual(m1.Currency, m2.Currency);
        }

        [Fact]
        public void IsSameAmout_ShouldReturnTrue()
        {
            // Arrange
            Money m1 = new Money(50, "EUR");
            Money m2 = new Money(50, "USD");

            // Act & Assert
            Assert.NotEqual(m1.Amount, m2.Amount);
        }
    }
}

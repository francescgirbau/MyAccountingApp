using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Repositories;

public class InMemoryConversionRepositoryTests
{
    [Fact]
    public void AddOrUpdate_ShouldAddConversion_WhenNew()
    {
        // Arrange
        InMemoryConversionRepository repo = new InMemoryConversionRepository();
        Conversion conv = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        repo.AddOrUpdate(conv);

        // Assert
        Conversion result = repo.GetAll().Single();
        Assert.Equal(conv, result);
    }

    [Fact]
    public void AddOrUpdate_ShouldReplaceConversion_WhenSameDate()
    {
        // Arrange
        InMemoryConversionRepository repo = new InMemoryConversionRepository();
        Conversion conv1 = new Conversion(DateTime.Today, Currencies.EUR);
        Conversion conv2 = new Conversion(DateTime.Today, Currencies.EUR, new() { { Currencies.USD, 1.1 } });

        repo.AddOrUpdate(conv1);

        // Act
        repo.AddOrUpdate(conv2);

        // Assert
        Conversion result = repo.GetAll().Single();
        Assert.Equal(conv2, result);
        Assert.True(result.TryGetQuote(Currencies.USD, out _));
    }

    [Fact]
    public void GetByDate_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        InMemoryConversionRepository repo = new InMemoryConversionRepository();

        // Act
        Conversion? result = repo.GetByDate(DateTime.Today);

        // Assert
        Assert.Null(result);
    }
}

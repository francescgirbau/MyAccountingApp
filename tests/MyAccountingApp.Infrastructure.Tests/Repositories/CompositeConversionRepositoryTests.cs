using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Infrastructure.Repositories;

namespace MyAccountingApp.Infrastructure.Tests.Repositories;

public class CompositeConversionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public CompositeConversionRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void AddOrUpdate_ShouldSaveInMemoryRepository()
    {
        // Arrange
        CompositeConversionRepository repo = new CompositeConversionRepository(this._tempFile);
        Conversion conv = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        repo.AddOrUpdate(conv);

        // Assert
        Conversion? conversion = repo.GetByDate(DateTime.Today);
        Assert.NotNull(conversion);
    }

    [Fact]
    public void AddOrUpdate_ShouldSaveJSONRepository()
    {
        // Arrange
        CompositeConversionRepository repo = new CompositeConversionRepository(this._tempFile);
        Conversion conv = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        repo.AddOrUpdate(conv);
        CompositeConversionRepository reLoaded = new CompositeConversionRepository(this._tempFile);

        // Assert
        Conversion? conversion = reLoaded.GetByDate(DateTime.Today);
        Assert.NotNull(conversion);
    }

    [Fact]
    public void Initialize_ShouldPopulateInMemoryRepository()
    {
        CompositeConversionRepository repo = new CompositeConversionRepository(this._tempFile);
        Conversion[] convs = new[]
        {
            new Conversion(DateTime.Today, Currencies.EUR),
            new Conversion(DateTime.Today.AddDays(-1), Currencies.EUR),
        };

        // Act
        repo.Initialize(convs);

        // Assert
        Assert.Equal(2, repo.GetAll().Count());
    }

    [Fact]
    public void Initialize_ShouldPopulateInJSONRepository()
    {
        CompositeConversionRepository repo = new CompositeConversionRepository(this._tempFile);
        Conversion[] convs = new[]
        {
            new Conversion(DateTime.Today, Currencies.EUR),
            new Conversion(DateTime.Today.AddDays(-1), Currencies.EUR),
        };

        // Act
        repo.Initialize(convs);
        CompositeConversionRepository reLoaded = new CompositeConversionRepository(this._tempFile);

        // Assert
        Assert.Equal(2, reLoaded.GetAll().Count());
    }
}

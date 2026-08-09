using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonConversionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonConversionRepositoryTests()
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
    public void AddOrUpdate_ShouldPersistConversion()
    {
        // Arrange
        JsonConversionRepository repo = new JsonConversionRepository(this._tempFile);
        Conversion conv = new Conversion(DateTime.Today, Currencies.EUR);

        // Act
        repo.AddOrUpdate(conv);

        // Assert
        Conversion? loaded = repo.GetByDate(DateTime.Today);
        Assert.NotNull(loaded);
        Assert.Equal(conv.Date, loaded!.Date);
    }

    [Fact]
    public void GetLatestOnOrBefore_ShouldReturnMostRecentConversion_OnOrBeforeDate()
    {
        // Arrange
        JsonConversionRepository repo = new JsonConversionRepository(this._tempFile);
        repo.Initialize(new[]
        {
            new Conversion(new DateTime(2026, 7, 1), Currencies.EUR),
            new Conversion(new DateTime(2026, 7, 15), Currencies.EUR),
            new Conversion(new DateTime(2026, 8, 1), Currencies.EUR),
        });

        // Act
        Conversion? result = repo.GetLatestOnOrBefore(new DateTime(2026, 7, 20));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 7, 15), result.Date);
    }

    [Fact]
    public void GetLatestOnOrBefore_ShouldReturnNull_WhenAllConversionsAfterDate()
    {
        // Arrange
        JsonConversionRepository repo = new JsonConversionRepository(this._tempFile);
        repo.Initialize(new[]
        {
            new Conversion(new DateTime(2026, 8, 1), Currencies.EUR),
        });

        // Act
        Conversion? result = repo.GetLatestOnOrBefore(new DateTime(2026, 7, 31));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_ShouldReturnEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        string path = Path.Combine(this._tempFile, Guid.NewGuid().ToString());
        JsonConversionRepository repo = new JsonConversionRepository(path);

        // Act
        IEnumerable<Conversion> result = repo.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Initialize_ShouldOverwriteExistingData()
    {
        // Arrange
        JsonConversionRepository repo = new JsonConversionRepository(this._tempFile);
        Conversion[] convs = new[]
        {
            new Conversion(DateTime.Today, Currencies.EUR),
            new Conversion(DateTime.Today.AddDays(-1), Currencies.EUR),
        };

        // Act
        repo.Initialize(convs);

        // Assert
        IEnumerable<Conversion> all = repo.GetAll();
        Assert.Equal(2, all.Count());
    }
}

using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonConversionRepositoryMigrationTests : IDisposable
{
    private readonly string _tempFile;

    public JsonConversionRepositoryMigrationTests()
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
    public void GetByDate_LoadsLegacyFormat_WithoutNewFields()
    {
        // Arrange
        string legacyJson = """[{"Date":"2022-06-15T00:00:00","Source":"EUR","Quotes":{"USD":1.05,"CAD":1.3}}]""";
        File.WriteAllText(this._tempFile, legacyJson);
        JsonConversionRepository repo = new(this._tempFile);

        // Act
        Conversion? conversion = repo.GetByDate(new DateTime(2022, 6, 15));

        // Assert
        Assert.NotNull(conversion);
        Assert.Equal(1.05m, conversion!.Quotes[Currencies.USD]);
        Assert.Equal(1.3m, conversion.Quotes[Currencies.CAD]);
        Assert.False(conversion.IsStale);
        Assert.Equal("exchangerate.host", conversion.SourceProvider);
        Assert.Equal(new DateTime(2022, 6, 15), conversion.RetrievedAtUtc);
    }

    [Fact]
    public void AddOrUpdate_RoundTripsNewFields()
    {
        // Arrange
        JsonConversionRepository repo = new(this._tempFile);
        Dictionary<Currencies, decimal> quotes = new() { { Currencies.USD, 1.1m } };
        Conversion conversion = new(
            new DateTime(2023, 1, 1),
            Currencies.EUR,
            quotes,
            new DateTime(2023, 1, 1, 10, 30, 0),
            isStale: true,
            sourceProvider: "exchangerate.host");

        // Act
        repo.AddOrUpdate(conversion);
        Conversion? loaded = repo.GetByDate(new DateTime(2023, 1, 1));

        // Assert
        Assert.NotNull(loaded);
        Assert.True(loaded!.IsStale);
        Assert.Equal("exchangerate.host", loaded.SourceProvider);
        Assert.Equal(new DateTime(2023, 1, 1, 10, 30, 0), loaded.RetrievedAtUtc);
    }
}

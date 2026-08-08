using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonPendingConversionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonPendingConversionRepositoryTests()
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
    public void GetAll_ReturnsEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        JsonPendingConversionRepository repo = new(path);

        // Act
        IEnumerable<PendingConversionRequest> result = repo.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AddOrUpdate_ThenGetAll_RoundTrips()
    {
        // Arrange
        JsonPendingConversionRepository repo = new(this._tempFile);
        PendingConversionRequest request = new(new DateOnly(2026, 7, 1), Currencies.EUR, DateTime.UtcNow);

        // Act
        repo.AddOrUpdate(request);
        IEnumerable<PendingConversionRequest> loaded = repo.GetAll();

        // Assert
        PendingConversionRequest item = Assert.Single(loaded);
        Assert.Equal(new DateOnly(2026, 7, 1), item.Date);
        Assert.Equal(PendingStatus.Pending, item.Status);
    }

    [Fact]
    public void AddOrUpdate_SameDate_UpdatesExisting()
    {
        // Arrange
        JsonPendingConversionRepository repo = new(this._tempFile);
        PendingConversionRequest failed = new(new DateOnly(2026, 7, 1), Currencies.EUR, DateTime.UtcNow);
        failed.MarkFailed("boom");

        // Act
        repo.AddOrUpdate(failed);
        repo.AddOrUpdate(new PendingConversionRequest(new DateOnly(2026, 7, 1), Currencies.EUR, DateTime.UtcNow));

        // Assert
        PendingConversionRequest loaded = Assert.Single(repo.GetAll());
        Assert.Equal(PendingStatus.Pending, loaded.Status);
        Assert.Null(loaded.LastError);
    }
}

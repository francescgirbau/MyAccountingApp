using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Tests.Services;

public class ApiQuotaManagerTests : IDisposable
{
    private readonly string _tempFile;

    public ApiQuotaManagerTests()
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
    public async Task TryConsumeAsync_ReturnsTrue_AndSaves_WhenQuotaAvailable()
    {
        // Arrange
        JsonApiQuotaRepository repo = new(this._tempFile);
        ApiQuotaManager manager = new(repo);

        // Act
        bool result = await manager.TryConsumeAsync();

        // Assert
        Assert.True(result);
        Assert.Equal(1, repo.Get().RequestsUsed);
    }

    [Fact]
    public async Task TryConsumeAsync_ReturnsFalse_WhenQuotaExhausted()
    {
        // Arrange
        JsonApiQuotaRepository repo = new(this._tempFile);
        ApiQuotaManager manager = new(repo);
        ApiUsageQuota exhausted = repo.Get();
        exhausted.MarkExhausted();
        repo.Save(exhausted);

        // Act
        bool result = await manager.TryConsumeAsync();

        // Assert
        Assert.False(result);
        Assert.Equal(100, repo.Get().RequestsUsed);
    }

    [Fact]
    public async Task MarkExhaustedAsync_SavesQuotaAtLimit()
    {
        // Arrange
        JsonApiQuotaRepository repo = new(this._tempFile);
        ApiQuotaManager manager = new(repo);

        // Act
        await manager.MarkExhaustedAsync();

        // Assert
        Assert.Equal(100, repo.Get().RequestsUsed);
        Assert.False(repo.Get().CanConsume());
    }

    [Fact]
    public async Task GetQuotaAsync_ReturnsCurrentQuota()
    {
        // Arrange
        JsonApiQuotaRepository repo = new(this._tempFile);
        ApiQuotaManager manager = new(repo);

        // Act
        ApiUsageQuota quota = await manager.GetQuotaAsync();

        // Assert
        Assert.Equal("exchangerate.host", quota.Provider);
        Assert.Equal(0, quota.RequestsUsed);
        Assert.Equal(90, quota.Available);
    }
}

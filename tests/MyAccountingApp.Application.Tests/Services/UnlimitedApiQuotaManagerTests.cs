using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Tests.Services;

public class UnlimitedApiQuotaManagerTests
{
    private readonly UnlimitedApiQuotaManager _manager = new("frankfurter");

    [Fact]
    public async Task TryConsumeAsync_AlwaysReturnsTrue()
    {
        // Act
        bool result = await this._manager.TryConsumeAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetQuotaAsync_ReportsUnlimitedLimit()
    {
        // Act
        ApiUsageQuota quota = await this._manager.GetQuotaAsync();

        // Assert
        Assert.Equal("frankfurter", quota.Provider);
        Assert.Equal(0, quota.RequestsUsed);
        Assert.Equal(int.MaxValue, quota.RequestsLimit);
        Assert.Equal(0, quota.SafetyMargin);
    }

    [Fact]
    public async Task MarkExhaustedAsync_DoesNotThrow()
    {
        // Act & Assert
        await this._manager.MarkExhaustedAsync();
        Assert.True(await this._manager.TryConsumeAsync(1));
    }

    [Fact]
    public async Task EnsurePeriodAsync_DoesNotThrow()
    {
        // Act & Assert
        await this._manager.EnsurePeriodAsync();
        Assert.True(await this._manager.TryConsumeAsync(1));
    }
}

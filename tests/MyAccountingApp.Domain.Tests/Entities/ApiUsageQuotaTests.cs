using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Tests.Entities;

public class ApiUsageQuotaTests
{
    [Fact]
    public void CanConsume_ReturnsTrue_WhenWithinLimit()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 50, limit: 100, margin: 10);

        // Act & Assert
        Assert.True(quota.CanConsume());
        Assert.Equal(40, quota.Available);
    }

    [Fact]
    public void CanConsume_ReturnsFalse_WhenQuotaExhausted()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 95, limit: 100, margin: 10);

        // Act & Assert
        Assert.False(quota.CanConsume());
        Assert.Equal(0, quota.Available);
    }

    [Fact]
    public void CanConsume_ReturnsFalse_WhenCostExceedsAvailable()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 85, limit: 100, margin: 10);

        // Act & Assert
        Assert.False(quota.CanConsume(cost: 10));
        Assert.True(quota.CanConsume(cost: 5));
    }

    [Fact]
    public void RegisterUsage_IncrementsRequestsUsed()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 10, limit: 100, margin: 10);

        // Act
        quota.RegisterUsage(5);

        // Assert
        Assert.Equal(15, quota.RequestsUsed);
    }

    [Fact]
    public void RegisterUsage_DoesNotExceedLimit()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 99, limit: 100, margin: 10);

        // Act
        quota.RegisterUsage(10);

        // Assert
        Assert.Equal(100, quota.RequestsUsed);
        Assert.Equal(0, quota.Available);
    }

    [Fact]
    public void MarkExhausted_SetsUsedToLimit()
    {
        // Arrange
        ApiUsageQuota quota = CreateQuota(used: 30, limit: 100, margin: 10);

        // Act
        quota.MarkExhausted();

        // Assert
        Assert.Equal(100, quota.RequestsUsed);
        Assert.False(quota.CanConsume());
    }

    [Fact]
    public void EnsureCurrentPeriod_ResetsUsage_WhenNewPeriod()
    {
        // Arrange
        ApiUsageQuota quota = new("test", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 80, 100, 10, DateTime.UtcNow);

        // Act
        quota.EnsureCurrentPeriod(new DateOnly(2026, 7, 10));

        // Assert
        Assert.Equal(new DateOnly(2026, 7, 1), quota.PeriodStart);
        Assert.Equal(new DateOnly(2026, 7, 31), quota.PeriodEnd);
        Assert.Equal(0, quota.RequestsUsed);
        Assert.Equal(90, quota.Available);
    }

    [Fact]
    public void EnsureCurrentPeriod_KeepsUsage_WhenSamePeriod()
    {
        // Arrange
        ApiUsageQuota quota = new("test", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 40, 100, 10, DateTime.UtcNow);

        // Act
        quota.EnsureCurrentPeriod(new DateOnly(2026, 7, 10));

        // Assert
        Assert.Equal(40, quota.RequestsUsed);
        Assert.Equal(new DateOnly(2026, 7, 1), quota.PeriodStart);
    }

    private static ApiUsageQuota CreateQuota(int used, int limit, int margin)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return new ApiUsageQuota("test", today, today.AddMonths(1).AddDays(-1), used, limit, margin, DateTime.UtcNow);
    }
}

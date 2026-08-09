using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Api.Tests.Fakes;

public sealed class FakeCurrencyRateService : ICurrencyRateService
{
    public Task<Dictionary<Currencies, decimal>> GetQuotes(DateTime date)
    {
        return Task.FromResult(new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } });
    }

    public Task<Conversion> GetConversionAsync(DateTime date)
    {
        Conversion conversion = new(date, Currencies.EUR);
        conversion.AddOrUpdateQuote(Currencies.USD, 1.1m);
        return Task.FromResult(conversion);
    }

    public Task<bool> SyncRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<PendingProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PendingProcessingResult(2, 1, 0));
    }

    public Task<bool> BackfillIfEmptyAsync(int days, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<int> SyncGapAsync(int maxDays, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(3);
    }

    public Task<ConversionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConversionStatus("frankfurter", 10, new DateTime(2026, 8, 1), 2));
    }

    public Task<ApiUsageQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ApiUsageQuota("frankfurter", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 0, 100, 10, DateTime.UtcNow));
    }
}

using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class ConversionEndpoints
{
    public static void MapConversionEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/conversions", async (IConversionRepository repo, ICurrencyRateService currencyRateService, DateTime? date) =>
        {
            if (date.HasValue)
            {
                try
                {
                    Conversion conversion = await currencyRateService.GetConversionAsync(date.Value);
                    return Results.Ok(conversion.ToDto());
                }
                catch (ConversionNotAvailableException)
                {
                    return Results.NotFound(new { date = date.Value, message = "No conversion available for this date" });
                }
            }

            List<ConversionDto> conversions = repo.GetAll().Select(c => c.ToDto()).ToList();
            return Results.Ok(conversions);
        });

        app.MapGet($"{prefix}/conversions/quota", async (ICurrencyRateService currencyRateService, IPendingConversionQueue pendingQueue) =>
        {
            ApiUsageQuota quota = await currencyRateService.GetQuotaAsync();
            IReadOnlyList<PendingConversionRequest> pending = await pendingQueue.GetPendingAsync();
            return Results.Ok(new
            {
                provider = quota.Provider,
                requestsUsed = quota.RequestsUsed,
                requestsLimit = quota.RequestsLimit,
                safetyMargin = quota.SafetyMargin,
                available = quota.Available,
                periodStart = quota.PeriodStart,
                periodEnd = quota.PeriodEnd,
                pendingCount = pending.Count,
            });
        });

        app.MapGet($"{prefix}/conversions/status", async (ICurrencyRateService currencyRateService) =>
        {
            ConversionStatus status = await currencyRateService.GetStatusAsync();
            return Results.Ok(new
            {
                provider = status.Provider,
                cachedDays = status.CachedDays,
                lastCachedDate = status.LastCachedDate,
                pendingCount = status.PendingCount,
            });
        });

        app.MapPost($"{prefix}/conversions/sync", async (SyncConversionsRequest? request, ICurrencyRateService currencyRateService) =>
        {
            DateOnly start = DateOnly.FromDateTime(request?.From ?? DateTime.UtcNow.AddDays(-7));
            DateOnly end = DateOnly.FromDateTime(request?.To ?? DateTime.UtcNow.Date);

            if (end < start)
            {
                return Results.BadRequest(new { message = "'to' must be greater than or equal to 'from'" });
            }

            bool synced = await currencyRateService.SyncRangeAsync(start, end);
            return synced
                ? Results.Ok(new { message = "Range synced", start, end })
                : Results.Conflict(new { message = "No API quota available to sync the range" });
        });

        app.MapPost($"{prefix}/conversions/process-pending", async (ICurrencyRateService currencyRateService) =>
        {
            PendingProcessingResult result = await currencyRateService.ProcessPendingAsync();
            return Results.Ok(new { processedDays = result.ProcessedDays, requestsSpent = result.RequestsSpent, failures = result.Failures });
        });

        app.MapGet($"{prefix}/summary", (IAnnualSummaryService summaryService) =>
        {
            List<AnnualSummaryDto> summaries = summaryService.GetAll();
            return Results.Ok(summaries);
        });

        app.MapGet($"{prefix}/summary/{{year:int}}", (int year, IAnnualSummaryService summaryService) =>
        {
            AnnualSummaryDto? summary = summaryService.GetByYear(year);
            return summary is not null ? Results.Ok(summary) : Results.NotFound(new { year, message = "No data found for this year" });
        });
    }
}

record SyncConversionsRequest(DateTime? From, DateTime? To);

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class DataQualityEndpoints
{
    public static void MapDataQualityEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapPost($"{prefix}/data-quality/transfer-matches/recalculate", (ITransferMatchingService matchingService) =>
        {
            TransferMatchingResult result = matchingService.Recalculate();
            return Results.Ok(result);
        });

        app.MapPost($"{prefix}/data-quality/sync-missing-fx", async (IValidationQuery validationQuery, ICurrencyRateService rateService) =>
        {
            ValidationResult result = validationQuery.ValidateAll();
            List<DateOnly> dates = result.Warnings
                .Where(w => w.Field == "MISSING_FX" && w.Date is not null)
                .Select(w => w.Date!.Value)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            int synced = await rateService.SyncDatesAsync(dates);
            return Results.Ok(new { requestedDates = dates.Count, syncedDates = synced });
        });
    }
}
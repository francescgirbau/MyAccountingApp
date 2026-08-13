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
    }
}
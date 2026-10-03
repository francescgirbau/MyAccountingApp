using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;

namespace MyAccountingApp.Api.Endpoints;

public static class IBKRDataHealthEndpoints
{
    public static void MapIBKRDataHealthEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/ibkr/data-health", (IIBKRDataHealthService service) =>
        {
            return Results.Ok(service.GetReport());
        });

        app.MapPost($"{prefix}/ibkr/rebuild/preview", (IBKRRebuildRequest request, IIBKRRebuildService service) =>
        {
            if (request.Years is null || request.Years.Length == 0)
            {
                return Results.BadRequest(new { error = "At least one year is required." });
            }

            return Results.Ok(service.Preview(request.Years));
        });

        app.MapPost($"{prefix}/ibkr/rebuild", (IBKRRebuildRequest request, IIBKRRebuildService service) =>
        {
            if (request.Years is null || request.Years.Length == 0)
            {
                return Results.BadRequest(new { error = "At least one year is required." });
            }

            return Results.Ok(service.Rebuild(request.Years));
        });
    }
}
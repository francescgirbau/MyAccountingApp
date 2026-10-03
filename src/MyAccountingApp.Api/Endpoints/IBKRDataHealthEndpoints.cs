using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.Interfaces;

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
    }
}
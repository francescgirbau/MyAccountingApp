using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/dashboard", async (DateOnly? asOf, IDashboardQuery dashboardQuery) =>
        {
            DateOnly date = asOf ?? DateOnly.FromDateTime(DateTime.Today);
            DashboardDto dashboard = await dashboardQuery.GetAsync(date);
            return Results.Ok(dashboard);
        });
    }
}
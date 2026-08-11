using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/reports/realized-gains", async (int year, IRealizedGainsReportService reportService) =>
        {
            RealizedGainsReportDto report = await reportService.GetRealizedGainsAsync(year);
            return Results.Ok(report);
        });

        app.MapGet($"{prefix}/reports/withholding", async (int year, IRealizedGainsReportService reportService) =>
        {
            WithholdingReportDto report = await reportService.GetWithholdingAsync(year);
            return Results.Ok(report);
        });
    }
}

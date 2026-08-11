using Microsoft.AspNetCore.Builder;

namespace MyAccountingApp.Api.Endpoints;

public static class ApiEndpoints
{
    public const string ApiPrefix = "/api";

    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet($"{ApiPrefix}/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        app.MapTransactionsEndpoints();
        app.MapImportEndpoints();
        app.MapPortfolioEndpoints();
        app.MapConversionEndpoints();
        app.MapBackupEndpoints();
        app.MapDashboardEndpoints();
        app.MapReportsEndpoints();
    }
}

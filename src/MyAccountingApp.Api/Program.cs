using MyAccountingApp.Api;
using MyAccountingApp.Api.Endpoints;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/myaccountingapp-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting MyAccountingApp API");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.AddApplicationServices();

    WebApplication app = builder.Build();
    app.UseApiPipeline();
    app.MapApiEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}

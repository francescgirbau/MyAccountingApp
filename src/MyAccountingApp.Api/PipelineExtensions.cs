using Microsoft.AspNetCore.Diagnostics;
using Serilog;

namespace MyAccountingApp.Api;

public static class PipelineExtensions
{
    public static void UseApiPipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                IExceptionHandlerPathFeature? exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                Exception? error = exceptionHandlerPathFeature?.Error;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = error?.Message ?? "An unexpected error occurred",
                    type = error?.GetType().Name,
                });
            });
        });

        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI();

        string webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (Directory.Exists(webRootPath))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapFallbackToFile("index.html");
    }
}

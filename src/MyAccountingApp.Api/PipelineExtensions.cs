using Microsoft.AspNetCore.Diagnostics;
using MyAccountingApp.Core.Vault;
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

        app.Use(async (context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
                {
                    IWebHostEnvironment? env = context.RequestServices.GetService<IWebHostEnvironment>();
                    if (env == null || !env.IsEnvironment("Testing"))
                    {
                        IVaultService? vault = context.RequestServices.GetService<IVaultService>();
                        if (vault != null && vault.IsInitialized && !vault.IsUnlocked)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new { message = "Vault is locked. Please unlock." });
                            return;
                        }
                    }
                }
            }

            await next();
        });

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

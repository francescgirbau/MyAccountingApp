using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyAccountingApp.Core.Vault;

namespace MyAccountingApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app, string prefix = "/api")
    {
        app.MapGet($"{prefix}/auth/status", (IVaultService vault) =>
        {
            return Results.Ok(new
            {
                isInitialized = vault.IsInitialized,
                isUnlocked = vault.IsUnlocked,
            });
        });

        app.MapPost($"{prefix}/auth/setup", (AuthRequest request, IVaultService vault) =>
        {
            if (vault.IsInitialized)
            {
                return Results.BadRequest(new { message = "Vault is already initialized." });
            }

            if (string.IsNullOrWhiteSpace(request?.Password) || request.Password.Length < 4)
            {
                return Results.BadRequest(new { message = "Password must be at least 4 characters long." });
            }

            try
            {
                vault.Initialize(request.Password);
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost($"{prefix}/auth/unlock", (AuthRequest request, IVaultService vault) =>
        {
            if (!vault.IsInitialized)
            {
                return Results.BadRequest(new { message = "Vault is not initialized." });
            }

            bool success = vault.Unlock(request?.Password ?? string.Empty);
            if (!success)
            {
                return Results.BadRequest(new { success = false, message = "Invalid password." });
            }

            return Results.Ok(new { success = true });
        });

        app.MapPost($"{prefix}/auth/lock", (IVaultService vault) =>
        {
            vault.Lock();
            return Results.Ok(new { success = true });
        });
    }

    public record AuthRequest(string Password);
}

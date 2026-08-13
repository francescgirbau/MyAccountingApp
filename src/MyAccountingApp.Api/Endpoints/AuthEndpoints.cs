using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyAccountingApp.Core.Persistence;
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
                isEnabled = vault.IsEnabled,
                isInitialized = vault.IsInitialized,
                isUnlocked = vault.IsUnlocked,
            });
        });

        app.MapPost($"{prefix}/auth/setup", (AuthRequest request, IVaultService vault) =>
        {
            if (!vault.IsEnabled)
            {
                return Results.BadRequest(new { message = "The vault is disabled." });
            }

            if (vault.IsInitialized)
            {
                return Results.BadRequest(new { message = "Vault is already initialized." });
            }

            if (string.IsNullOrWhiteSpace(request?.Password) || request.Password.Length < 12)
            {
                return Results.BadRequest(new { message = "Password must be at least 12 characters long." });
            }

            vault.Initialize(request.Password);
            return Results.Ok(new { success = true });
        });

        app.MapPost($"{prefix}/auth/unlock", (AuthRequest request, IVaultService vault, IVaultSessionListener sessionListener) =>
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

            sessionListener.OnUnlocked();
            return Results.Ok(new { success = true });
        });

        app.MapPost($"{prefix}/auth/lock", (IVaultService vault, IVaultSessionListener sessionListener) =>
        {
            if (vault.IsInitialized)
            {
                vault.Lock();
                sessionListener.OnLocked();
            }

            return Results.Ok(new { success = true });
        });
    }

    public record AuthRequest(string Password);
}

using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/portfolio", async (IPortfolioRepository repo, IPositionEngine positionEngine) =>
        {
            string[] symbols = repo.GetAllTransactions().Select(t => t.Symbol).Distinct().ToArray();
            PortfolioPositionDto?[] positions = await Task.WhenAll(symbols.Select(s => positionEngine.GetPosition(s)));
            return Results.Ok(positions.Where(p => p is not null).ToList());
        });

        app.MapGet($"{prefix}/portfolio/{{symbol}}", async (string symbol, IPositionEngine positionEngine) =>
        {
            PortfolioPositionDto? position = await positionEngine.GetPosition(symbol);
            return position is not null ? Results.Ok(position) : Results.NotFound(new { symbol, message = "No transactions found for this symbol" });
        });

        app.MapGet($"{prefix}/validate", (IValidationQuery validationQuery) =>
        {
            ValidationResult result = validationQuery.ValidateAll();
            return Results.Ok(new
            {
                isValid = result.IsValid,
                errorCount = result.Errors.Count,
                warningCount = result.Warnings.Count,
                errors = result.Errors,
                warnings = result.Warnings,
            });
        });
    }
}

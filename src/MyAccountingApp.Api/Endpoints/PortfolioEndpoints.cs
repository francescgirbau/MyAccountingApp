using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class PortfolioEndpoints
{
    private static string[] GetSymbolUnion(IPortfolioRepository repo, IOptionTransactionRepository optionRepo)
    {
        return repo.GetAllTransactions().Select(t => t.Symbol)
            .Union(optionRepo.GetAll().Select(o => o.Symbol))
            .Distinct()
            .ToArray();
    }

    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/portfolio", async (IPortfolioRepository repo, IOptionTransactionRepository optionRepo, IPositionEngine positionEngine, bool includePrices = false) =>
        {
            string[] symbols = GetSymbolUnion(repo, optionRepo);
            PortfolioPositionDto?[] positions = await Task.WhenAll(symbols.Select(s => positionEngine.GetPosition(s, includePrices)));
            return Results.Ok(positions.Where(p => p is not null).ToList());
        });

        app.MapGet($"{prefix}/portfolio/overview", async (IPortfolioOverviewQuery overviewQuery) =>
        {
            PortfolioOverviewDto overview = await overviewQuery.GetOverviewAsync(DateOnly.FromDateTime(DateTime.UtcNow));
            return Results.Ok(overview);
        });

        app.MapGet($"{prefix}/portfolio/{{symbol}}", async (string symbol, IPositionEngine positionEngine, bool includePrices = true) =>
        {
            PortfolioPositionDto? position = await positionEngine.GetPosition(symbol, includePrices);
            return position is not null ? Results.Ok(position) : Results.NotFound(new { symbol, message = "No transactions found for this symbol" });
        });

        app.MapPost($"{prefix}/portfolio/refresh-prices", async (IPortfolioRepository repo, IOptionTransactionRepository optionRepo, IPositionEngine positionEngine, IMarketPriceService priceService) =>
        {
            string[] symbols = GetSymbolUnion(repo, optionRepo);
            await Task.WhenAll(symbols.Select(s => priceService.RefreshPriceAsync(s)));
            PortfolioPositionDto?[] positions = await Task.WhenAll(symbols.Select(s => positionEngine.GetPosition(s, true)));
            return Results.Ok(positions.Where(p => p is not null).ToList());
        });

        app.MapGet($"{prefix}/portfolio/valuation", async (IPositionValuationService valuationService, DateTime? asOf) =>
        {
            DateOnly valuationDate = DateOnly.FromDateTime((asOf ?? DateTime.UtcNow).Date);
            IReadOnlyList<PositionValuationDto> valuations = await valuationService.GetValuationsAsync(valuationDate);
            return Results.Ok(new { asOf = valuationDate, positions = valuations });
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

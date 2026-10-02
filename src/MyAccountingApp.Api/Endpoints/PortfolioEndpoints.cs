using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class PortfolioEndpoints
{
    private static string[] GetSymbolUnion(IPortfolioRepository repo)
    {
        return repo.GetAllTransactions().Select(t => t.Symbol).Distinct().ToArray();
    }

    private static IReadOnlyDictionary<string, string> GetCurrencyBySymbol(IPortfolioRepository repo)
    {
        Dictionary<string, string> currencyBySymbol = new(StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, AssetTransaction> group in repo.GetAllTransactions().GroupBy(t => t.Symbol))
        {
            currencyBySymbol.TryAdd(group.Key, group.First().Transaction.Money.Currency);
        }

        return currencyBySymbol;
    }

    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/portfolio", async (IPortfolioRepository repo, IPositionEngine positionEngine, bool includePrices = false) =>
        {
            string[] symbols = GetSymbolUnion(repo);
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

        app.MapPost($"{prefix}/portfolio/refresh-prices", async (IPortfolioRepository repo, IPositionEngine positionEngine, IMarketPriceService priceService) =>
        {
            string[] symbols = GetSymbolUnion(repo);
            IReadOnlyDictionary<string, string> currencyBySymbol = GetCurrencyBySymbol(repo);
            await Task.WhenAll(symbols.Select(s => priceService.RefreshPriceAsync(s, currencyBySymbol.TryGetValue(s, out string? currency) ? currency : null)));
            PortfolioPositionDto?[] positions = await Task.WhenAll(symbols.Select(s => positionEngine.GetPosition(s, true)));
            return Results.Ok(positions.Where(p => p is not null).ToList());
        });

        app.MapGet($"{prefix}/portfolio/valuation", async (IPositionValuationService valuationService, DateTime? asOf) =>
        {
            DateOnly valuationDate = DateOnly.FromDateTime((asOf ?? DateTime.UtcNow).Date);
            IReadOnlyList<PositionValuationDto> valuations = await valuationService.GetValuationsAsync(valuationDate);
            return Results.Ok(new PositionValuationResponse(valuationDate, valuations));
        });

        app.MapGet($"{prefix}/portfolio/return", async (IPortfolioReturnService returnService) =>
        {
            PortfolioReturnDto result = await returnService.GetReturnAsync(DateOnly.FromDateTime(DateTime.UtcNow));
            return Results.Ok(result);
        });

        app.MapGet($"{prefix}/validate", (IValidationQuery validationQuery) =>
        {
            ValidationResult result = validationQuery.ValidateAll();
            return Results.Ok(new ValidationResponseDto(
                result.IsValid,
                result.Errors.Count,
                result.Warnings.Count,
                result.Errors,
                result.Warnings));
        });
    }
}

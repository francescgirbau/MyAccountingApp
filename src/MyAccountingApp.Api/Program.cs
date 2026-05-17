using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string currencyApiKey = builder.Configuration["CurrencyApi:ApiKey"]
    ?? Environment.GetEnvironmentVariable("CURRENCY_API_KEY")
    ?? throw new InvalidOperationException(
        "CurrencyApi:ApiKey not found. Set it in appsettings.json or the CURRENCY_API_KEY environment variable.");

CompositeConversionRepository repo = new CompositeConversionRepository("data/conversions.json");
CurrencyConverter api = new CurrencyConverter(currencyApiKey);
Currencies source = Currencies.EUR;
CurencyRateService currencyRateService = new CurencyRateService(repo, api, source);

builder.Services.AddSingleton<IConversionRepository>(repo);
builder.Services.AddSingleton<ICurrencyRateService>(currencyRateService);
builder.Services.AddSingleton<ITransactionRepository>(
    new CompositeTransactionRepository("data/transactions.json"));
builder.Services.AddSingleton<IPortfolioRepository>(
    new CompositePortfolioRepository("data/portfolio.json"));
builder.Services.AddSingleton<IBrokerImportService>(sp =>
{
    ICsvParser csvParser = new InteractiveBrokersCsvParser();
    ILogger<InteractiveBrokersImportService> logger = sp.GetRequiredService<ILogger<InteractiveBrokersImportService>>();
    return new InteractiveBrokersImportService(csvParser, logger);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IMarketPriceService, YahooMarketPriceService>();
builder.Services.AddSingleton<IImportService, ImportService>();
builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
builder.Services.AddSingleton<IPortfolioQuery, PortfolioQuery>();
builder.Services.AddSingleton<IPositionEngine, PositionEngine>();
builder.Services.AddSingleton<IValidationQuery, ValidationQuery>();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/transactions", (ITransactionRepository repo) =>
{
    List<TransactionDto> transactions = repo.GetAll().Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapGet("/asset-transactions", (IPortfolioRepository repo) =>
{
    List<AssetTransactionDto> transactions = repo.GetAllTransactions().Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapGet("/asset-transactions/{symbol}", (string symbol, IPortfolioRepository repo) =>
{
    List<AssetTransactionDto> transactions = repo.GetAssetTransactions(symbol).Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapPost("/import", async (ImportRequest request, IImportService importService) =>
{
    ImportResult result = await importService.ImportFromFoldersAsync(request.FolderPaths);
    return Results.Ok(result.ToDto());
});

app.MapGet("/portfolio/{symbol}", (string symbol, IPositionEngine positionEngine) =>
{
    PortfolioPositionDto? position = positionEngine.GetPosition(symbol);
    return position is not null ? Results.Ok(position) : Results.NotFound(new { symbol, message = "No transactions found for this symbol" });
});

app.MapGet("/validate", (IValidationQuery validationQuery) =>
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

app.MapGet("/conversions", (IConversionRepository repo, DateTime? date) =>
{
    if (date.HasValue)
    {
        var conversion = repo.GetByDate(date.Value);
        return conversion is not null ? Results.Ok(conversion.ToDto()) : Results.NotFound();
    }

    List<ConversionDto> conversions = repo.GetAll().Select(c => c.ToDto()).ToList();
    return Results.Ok(conversions);
});

app.Run();

record ImportRequest(List<string> FolderPaths);

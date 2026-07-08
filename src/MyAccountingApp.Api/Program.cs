using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

bool isDev = builder.Environment.IsDevelopment();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (isDev)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

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
builder.Services.AddSingleton<InteractiveBrokersImportService>(sp =>
{
    ICsvParser csvParser = new InteractiveBrokersCsvParser();
    ILogger<InteractiveBrokersImportService> logger = sp.GetRequiredService<ILogger<InteractiveBrokersImportService>>();
    return new InteractiveBrokersImportService(csvParser, logger);
});
builder.Services.AddSingleton<BankCsvImportService>();
builder.Services.AddSingleton<AssetTransactionCsvImportService>();
builder.Services.AddSingleton<IBrokerImportService, BrokerImportDispatcher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IMarketPriceService, YahooMarketPriceService>();
builder.Services.AddSingleton<IImportService, ImportService>();
builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
builder.Services.AddSingleton<IPortfolioQuery, PortfolioQuery>();
builder.Services.AddSingleton<IPositionEngine, PositionEngine>();
builder.Services.AddSingleton<IValidationQuery, ValidationQuery>();
builder.Services.AddSingleton<IAnnualSummaryService, AnnualSummaryService>();

WebApplication app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

string webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(webRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

const string prefix = "/api";

app.MapGet($"{prefix}/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet($"{prefix}/transactions", (ITransactionRepository repo) =>
{
    List<TransactionDto> transactions = repo.GetAll().Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapGet($"{prefix}/asset-transactions", (IPortfolioRepository repo) =>
{
    List<AssetTransactionDto> transactions = repo.GetAllTransactions().Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapGet($"{prefix}/asset-transactions/{{symbol}}", (string symbol, IPortfolioRepository repo) =>
{
    List<AssetTransactionDto> transactions = repo.GetAssetTransactions(symbol).Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapPost($"{prefix}/import", async (ImportRequest request, IImportService importService) =>
{
    ImportResult result = await importService.ImportFromFoldersAsync(request.FolderPaths);
    return Results.Ok(result.ToDto());
});

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

app.MapGet($"{prefix}/conversions", (IConversionRepository repo, DateTime? date) =>
{
    if (date.HasValue)
    {
        var conversion = repo.GetByDate(date.Value);
        return conversion is not null ? Results.Ok(conversion.ToDto()) : Results.NotFound();
    }

    List<ConversionDto> conversions = repo.GetAll().Select(c => c.ToDto()).ToList();
    return Results.Ok(conversions);
});

app.MapGet($"{prefix}/summary", (IAnnualSummaryService summaryService) =>
{
    List<AnnualSummaryDto> summaries = summaryService.GetAll();
    return Results.Ok(summaries);
});

app.MapGet($"{prefix}/summary/{{year:int}}", (int year, IAnnualSummaryService summaryService) =>
{
    AnnualSummaryDto? summary = summaryService.GetByYear(year);
    return summary is not null ? Results.Ok(summary) : Results.NotFound(new { year, message = "No data found for this year" });
});

app.MapFallbackToFile("index.html");

app.Run();

record ImportRequest(List<string> FolderPaths);

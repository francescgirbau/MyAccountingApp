using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
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
builder.Services.AddSingleton<IMarketPriceService, YahooMarketPriceService>();
builder.Services.AddSingleton<IImportService, ImportService>();
builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/transactions", (ITransactionRepository repo) =>
{
    IEnumerable<Transaction> transactions = repo.GetAll();
    return Results.Ok(transactions);
});

app.MapGet("/asset-transactions", (IPortfolioRepository repo) =>
{
    IEnumerable<AssetTransaction> transactions = repo.GetAllTransactions();
    return Results.Ok(transactions);
});

app.MapGet("/asset-transactions/{symbol}", (string symbol, IPortfolioRepository repo) =>
{
    IEnumerable<AssetTransaction> transactions = repo.GetAssetTransactions(symbol);
    return Results.Ok(transactions);
});

app.MapPost("/import", async (ImportRequest request, IImportService importService) =>
{
    ImportResult result = await importService.ImportFromFoldersAsync(request.FolderPaths);
    return Results.Ok(result);
});

app.MapGet("/portfolio/{symbol}", (string symbol, IPortfolioRepository repo) =>
{
    IEnumerable<AssetTransaction> transactions = repo.GetAssetTransactions(symbol);
    List<AssetTransaction> list = transactions.ToList();

    if (list.Count == 0)
    {
        return Results.NotFound(new { symbol, message = "No transactions found for this symbol" });
    }

    decimal netQuantity = 0;
    decimal totalCost = 0;
    string currency = list[0].Transaction.Money.Currency;

    foreach (AssetTransaction tx in list)
    {
        if (tx.Type == AssetTransactionType.Buy)
        {
            netQuantity += tx.Quantity;
            totalCost += tx.Transaction.Money.Amount;
        }
        else
        {
            netQuantity -= tx.Quantity;
            totalCost -= tx.Transaction.Money.Amount;
        }
    }

    decimal avgCost = netQuantity > 0 ? Math.Round(totalCost / netQuantity, 4) : 0;

    return Results.Ok(new
    {
        symbol,
        netQuantity,
        averageUnitaryCost = avgCost,
        totalCostBasis = Math.Round(totalCost, 2),
        currency,
        transactionCount = list.Count,
    });
});

app.MapGet("/validate", (ITransactionRepository txRepo, IPortfolioRepository pfRepo, ITransactionValidator validator) =>
{
    List<ValidationError> allErrors = new();
    List<ValidationError> allWarnings = new();

    foreach (Transaction tx in txRepo.GetAll())
    {
        ValidationResult vr = validator.Validate(tx);
        allErrors.AddRange(vr.Errors);
        allWarnings.AddRange(vr.Warnings);
    }

    foreach (AssetTransaction tx in pfRepo.GetAllTransactions())
    {
        ValidationResult vr = validator.Validate(tx);
        allErrors.AddRange(vr.Errors);
        allWarnings.AddRange(vr.Warnings);
    }

    return Results.Ok(new
    {
        isValid = allErrors.Count == 0,
        errorCount = allErrors.Count,
        warningCount = allWarnings.Count,
        errors = allErrors,
        warnings = allWarnings,
    });
});

app.MapGet("/conversions", (IConversionRepository repo, DateTime? date) =>
{
    if (date.HasValue)
    {
        Conversion? conversion = repo.GetByDate(date.Value);
        return conversion is not null ? Results.Ok(conversion) : Results.NotFound();
    }

    IEnumerable<Conversion> conversions = repo.GetAll();
    return Results.Ok(conversions);
});

app.Run();

record ImportRequest(List<string> FolderPaths);

using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

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

app.MapPost($"{prefix}/transactions", (CreateTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
{
    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
        return Results.BadRequest(validation.Errors);

    repo.AddOrUpdate(transaction);
    return Results.Created($"/api/transactions/{transaction.Id}", transaction.ToDto());
});

app.MapPut($"{prefix}/transactions/{{id:guid}}", (Guid id, CreateTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
{
    Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
    if (existing is null)
        return Results.NotFound(new { id, message = "Transaction not found" });

    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(id, request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
        return Results.BadRequest(validation.Errors);

    repo.AddOrUpdate(transaction);
    return Results.Ok(transaction.ToDto());
});

app.MapDelete($"{prefix}/transactions/{{id:guid}}", (Guid id, ITransactionRepository repo) =>
{
    Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
    if (existing is null)
        return Results.NotFound(new { id, message = "Transaction not found" });

    repo.Delete(existing);
    return Results.NoContent();
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

app.MapPost($"{prefix}/asset-transactions", (CreateAssetTransactionRequest request, IPortfolioRepository repo, ITransactionValidator validator) =>
{
    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
        return Results.BadRequest(validation.Errors);

    AssetTransactionType type = Enum.Parse<AssetTransactionType>(request.Type);
    AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
    repo.AddOrUpdate(assetTx);
    return Results.Created($"/api/asset-transactions/{transaction.Id}", assetTx.ToDto());
});

app.MapPut($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, CreateAssetTransactionRequest request, IPortfolioRepository repo, ITransactionValidator validator) =>
{
    AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
    if (existing is null)
        return Results.NotFound(new { id, message = "Asset transaction not found" });

    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(id, request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
        return Results.BadRequest(validation.Errors);

    AssetTransactionType type = Enum.Parse<AssetTransactionType>(request.Type);
    AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
    repo.AddOrUpdate(assetTx);
    return Results.Ok(assetTx.ToDto());
});

app.MapDelete($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, IPortfolioRepository repo) =>
{
    AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
    if (existing is null)
        return Results.NotFound(new { id, message = "Asset transaction not found" });

    repo.Delete(id);
    return Results.NoContent();
});

app.MapPost($"{prefix}/import", async (ImportRequest request, IImportService importService) =>
{
    ImportResult result = await importService.ImportFromFoldersAsync(request.FolderPaths);
    return Results.Ok(result.ToDto());
});

app.MapPost($"{prefix}/import/upload", async (HttpContext http, IImportService importService) =>
{
    IFormFile? file = http.Request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file provided" });

    string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(tempDir);
    string filePath = Path.Combine(tempDir, file.FileName);
    await using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    try
    {
        ImportResult result = await importService.ImportFromFoldersAsync(new[] { tempDir });
        return Results.Ok(result.ToDto());
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
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
record CreateTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category);
record CreateAssetTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category, string Symbol, decimal Quantity, string Type);

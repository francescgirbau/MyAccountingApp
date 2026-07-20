using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using System.Globalization;
using System.Linq;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/myaccountingapp-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting MyAccountingApp API");

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

builder.Host.UseSerilog();

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var error = exceptionHandlerPathFeature?.Error;
        await context.Response.WriteAsJsonAsync(new
        {
            error = error?.Message ?? "An unexpected error occurred",
            type = error?.GetType().Name,
        });
    });
});

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
    {
        return Results.BadRequest(validation.Errors);
    }

    repo.AddOrUpdate(transaction);
    return Results.Created($"/api/transactions/{transaction.Id}", transaction.ToDto());
});

app.MapPut($"{prefix}/transactions/{{id:guid}}", (Guid id, CreateTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
{
    Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new { id, message = "Transaction not found" });
    }

    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(id, request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation.Errors);
    }

    repo.AddOrUpdate(transaction);
    return Results.Ok(transaction.ToDto());
});

app.MapDelete($"{prefix}/transactions/{{id:guid}}", (Guid id, ITransactionRepository repo) =>
{
    Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new { id, message = "Transaction not found" });
    }

    repo.Delete(existing);
    return Results.NoContent();
});

app.MapDelete($"{prefix}/transactions/year/{{year:int}}", (int year, ITransactionRepository repo, IPortfolioRepository portfolioRepo) =>
{
    int transactionsRemoved = repo.DeleteByYear(year);
    int assetsRemoved = portfolioRepo.DeleteByYear(year);
    return Results.Ok(new { year, deletedTransactions = transactionsRemoved, deletedAssets = assetsRemoved });
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
    {
        return Results.BadRequest(validation.Errors);
    }

    AssetTransactionType type = Enum.Parse<AssetTransactionType>(request.Type);
    AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
    repo.AddOrUpdate(assetTx);
    return Results.Created($"/api/asset-transactions/{transaction.Id}", assetTx.ToDto());
});

app.MapPut($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, CreateAssetTransactionRequest request, IPortfolioRepository repo, ITransactionValidator validator) =>
{
    AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new { id, message = "Asset transaction not found" });
    }

    Money money = new(request.Amount, request.Currency);
    TransactionCategory category = Enum.Parse<TransactionCategory>(request.Category);
    Transaction transaction = new(id, request.Date, request.Description, money, category);

    ValidationResult validation = validator.Validate(transaction);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation.Errors);
    }

    AssetTransactionType type = Enum.Parse<AssetTransactionType>(request.Type);
    AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
    repo.AddOrUpdate(assetTx);
    return Results.Ok(assetTx.ToDto());
});

app.MapDelete($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, IPortfolioRepository repo) =>
{
    AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new { id, message = "Asset transaction not found" });
    }

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

// Raw CSV import: direct dump with minimal parsing, no transformations
app.MapPost($"{prefix}/import/raw-csv", async (
    HttpContext http,
    ITransactionRepository transactionRepo,
    ITransactionValidator validator,
    ILogger<Program> logger) =>
{
    IFormFile? file = http.Request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file provided" });

    using var reader = new StreamReader(file.OpenReadStream());
    string[] lines = await reader.ReadToEndAsync().ContinueWith(t => t.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    if (lines.Length < 2)
        return Results.BadRequest(new { error = "CSV must have a header row and at least one data row" });

    List<Transaction> parsed = new();
    List<string> errors = new();
    string defaultCurrency = "EUR";

    for (int i = 1; i < lines.Length; i++)
    {
        List<string> parts = BankCsvImportService.ParseCsvLine(lines[i]);
        if (parts.Count < 3)
        {
            errors.Add($"Line {i + 1}: expected at least 3 columns (Date,Description,Amount), got {parts.Count}");
            continue;
        }

        DateTime date;
        if (!DateTime.TryParse(parts[0], CultureInfo.CreateSpecificCulture("ca-ES"), DateTimeStyles.None, out date)
            && !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            errors.Add($"Line {i + 1}: invalid date '{parts[0]}'");
            continue;
        }

        string description = parts[1];

        decimal amount;
        if (!decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
        {
            errors.Add($"Line {i + 1}: invalid amount '{parts[2]}'");
            continue;
        }

        string currency = parts.Count >= 4 ? parts[3].ToUpperInvariant() : defaultCurrency;

        TransactionCategory category = amount >= 0 ? TransactionCategory.INCOME : TransactionCategory.EXPENSE;
        Money money = new Money(Math.Abs(amount), currency);
        Transaction transaction = new Transaction(date, description, money, category);

        ValidationResult vr = validator.Validate(transaction);
        if (!vr.IsValid)
        {
            errors.AddRange(vr.Errors.Select(e => $"Line {i + 1}: {e.Message}"));
            continue;
        }

        parsed.Add(transaction);
    }

    if (parsed.Count == 0)
    {
        return Results.Ok(new
        {
            imported = 0,
            skipped = 0,
            errors = errors,
            message = "No valid transactions found in CSV",
        });
    }

    List<Transaction> existing = transactionRepo.GetAll().ToList();
    existing.AddRange(parsed);
    transactionRepo.Initialize(existing);

    logger.LogInformation("Raw CSV import: {Imported} imported", parsed.Count);

    return Results.Ok(new
    {
        imported = parsed.Count,
        skipped = 0,
        errors = errors,
    });
});

// Clears cash transactions and portfolio (asset) data. Does not touch currency conversions.
app.MapPost($"{prefix}/data/reset", (ITransactionRepository transactionRepo, IPortfolioRepository portfolioRepo, ILogger<Program> logger) =>
{
    int transactionCount = transactionRepo.GetAll().Count();
    int assetTransactionCount = portfolioRepo.GetAllTransactions().Count();

    transactionRepo.Initialize(Array.Empty<Transaction>());
    portfolioRepo.Initialize(Array.Empty<AssetTransaction>());

    logger.LogWarning(
        "Data reset: cleared {TransactionCount} transactions and {AssetTransactionCount} asset transactions",
        transactionCount,
        assetTransactionCount);

    return Results.Ok(new
    {
        message = "Database reset completed",
        clearedTransactions = transactionCount,
        clearedAssetTransactions = assetTransactionCount,
    });
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

app.MapGet($"{prefix}/backup", (ITransactionRepository txRepo, IPortfolioRepository pfRepo) =>
{
    List<Transaction> transactions = txRepo.GetAll().ToList();
    List<AssetTransaction> assetTransactions = pfRepo.GetAllTransactions().ToList();
    string json = JsonSerializer.Serialize(new { transactions, assetTransactions }, new JsonSerializerOptions { WriteIndented = true });
    byte[] bytes = Encoding.UTF8.GetBytes(json);
    return Results.File(bytes, "application/json", $"myaccounting-backup-{DateTime.Now:yyyyMMdd}.json");
});

app.MapPost($"{prefix}/backup", async (HttpRequest request, ITransactionRepository txRepo, IPortfolioRepository pfRepo, ILogger<Program> logger) =>
{
    using StreamReader reader = new(request.Body);
    string body = await reader.ReadToEndAsync();

    try
    {
        var backup = JsonSerializer.Deserialize<BackupData>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (backup?.Transactions is null)
            return Results.BadRequest(new { error = "Invalid backup file format: 'transactions' array is required" });

        txRepo.Initialize(backup.Transactions);
        if (backup.AssetTransactions is not null)
            pfRepo.Initialize(backup.AssetTransactions);

        logger.LogInformation("Backup restored: {Count} transactions, {Count2} asset transactions",
            backup.Transactions.Count, backup.AssetTransactions?.Count ?? 0);
        return Results.Ok(new { message = $"Restored {backup.Transactions.Count} transactions and {backup.AssetTransactions?.Count ?? 0} asset transactions" });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
    }
});

app.MapFallbackToFile("index.html");

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

record ImportRequest(List<string> FolderPaths);
record CreateTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category);
record CreateAssetTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category, string Symbol, decimal Quantity, string Type);
record BackupData(List<Transaction> Transactions, List<AssetTransaction>? AssetTransactions);

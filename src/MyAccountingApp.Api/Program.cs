using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Agents.IBKR;
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
builder.Services.AddSingleton<IOptionTransactionRepository>(
    new JsonOptionTransactionRepository("data/options.json"));
builder.Services.AddSingleton<InteractiveBrokersImportService>(sp =>
{
    ICsvParser csvParser = new InteractiveBrokersCsvParser();
    ILogger<InteractiveBrokersImportService> logger = sp.GetRequiredService<ILogger<InteractiveBrokersImportService>>();
    return new InteractiveBrokersImportService(csvParser, logger);
});
builder.Services.AddSingleton<IBKRFlexQueryImportService>(sp =>
{
    IEnumerable<IIBKRStatementAgent> agents = sp.GetServices<IIBKRStatementAgent>();
    return new IBKRFlexQueryImportService(agents);
});
builder.Services.AddSingleton<IIBKRStatementAgent, TradeAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, DividendAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, DepositWithdrawalAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, CorporateActionAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, FeeAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, WithholdingTaxAgent>();
builder.Services.AddSingleton<IIBKRStatementAgent, InterestAgent>();
builder.Services.AddSingleton<BankCsvImportService>();
builder.Services.AddSingleton<AssetTransactionCsvImportService>();
builder.Services.AddSingleton<DegiroImportService>();
builder.Services.AddSingleton<DegiroTransactionImportService>();
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

app.MapGet($"{prefix}/transactions/year/{{year:int}}/count", (int year, ITransactionRepository repo, IPortfolioRepository portfolioRepo) =>
{
    int transactions = repo.GetAll().Count(t => t.Date.Year == year);
    int assets = portfolioRepo.GetAllTransactions().Count(a => a.Transaction.Date.Year == year);
    return Results.Ok(new { year, transactions, assets });
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

app.MapDelete($"{prefix}/asset-transactions/year/{{year:int}}", (int year, IPortfolioRepository portfolioRepo) =>
{
    int removed = portfolioRepo.DeleteByYear(year);
    return Results.Ok(new { year, deletedAssets = removed });
});

app.MapGet($"{prefix}/asset-transactions/year/{{year:int}}/count", (int year, IPortfolioRepository portfolioRepo) =>
{
    int assets = portfolioRepo.GetAllTransactions().Count(a => a.Transaction.Date.Year == year);
    return Results.Ok(new { year, assets });
});

app.MapGet($"{prefix}/option-transactions", (IOptionTransactionRepository repo) =>
{
    List<OptionTransactionDto> transactions = repo.GetAll().Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapGet($"{prefix}/option-transactions/{{symbol}}", (string symbol, IOptionTransactionRepository repo) =>
{
    List<OptionTransactionDto> transactions = repo.GetAll().Where(t => t.Symbol == symbol).Select(t => t.ToDto()).ToList();
    return Results.Ok(transactions);
});

app.MapPut($"{prefix}/option-transactions/{{id:guid}}", (Guid id, UpdateOptionTransactionRequest request, IOptionTransactionRepository repo) =>
{
    OptionTransaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new { id, message = "Option transaction not found" });
    }

    Money premium = new(request.PremiumAmount, request.PremiumCurrency);
    AssetTransactionType type = Enum.Parse<AssetTransactionType>(request.Type);
    OptionTransaction updated = new(
        id, request.Date, request.Description, request.Symbol, request.Isin, request.Quantity, premium, type);
    repo.Update(updated);
    return Results.Ok(updated.ToDto());
});

app.MapDelete($"{prefix}/option-transactions/{{id:guid}}", (Guid id, IOptionTransactionRepository repo) =>
{
    bool deleted = repo.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound(new { id, message = "Option transaction not found" });
});

app.MapDelete($"{prefix}/option-transactions/year/{{year:int}}", (int year, IOptionTransactionRepository repo) =>
{
    int removed = repo.DeleteByYear(year);
    return Results.Ok(new { year, deletedOptions = removed });
});

app.MapGet($"{prefix}/option-transactions/year/{{year:int}}/count", (int year, IOptionTransactionRepository repo) =>
{
    int count = repo.GetAll().Count(t => t.Date.Year == year);
    return Results.Ok(new { year, options = count });
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
        if (parts.Count < 4)
        {
            errors.Add($"Line {i + 1}: expected at least 4 columns (Date,Description,Amount,Type), got {parts.Count}");
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

        if (!Enum.TryParse<TransactionCategory>(parts[3], ignoreCase: true, out TransactionCategory category))
        {
            errors.Add($"Line {i + 1}: invalid type '{parts[3]}' (valid: Income, Expense, Transfer, Deposit)");
            continue;
        }

        string currency = parts.Count >= 5 ? parts[4].ToUpperInvariant() : defaultCurrency;
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
        pfRepo.Initialize(backup.AssetTransactions ?? []);

        logger.LogInformation("Backup restored: {Count} transactions, {Count2} asset transactions",
            backup.Transactions.Count, backup.AssetTransactions?.Count ?? 0);
        return Results.Ok(new { message = $"Restored {backup.Transactions.Count} transactions and {backup.AssetTransactions?.Count ?? 0} asset transactions" });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
    }
});

// Yahoo Finance symbol lookup by company name
app.MapGet($"{prefix}/symbol-lookup", async (string name) =>
{
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest(new { error = "Company name is required" });

    using HttpClient client = new();
    string url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(name)}&quotesCount=10";

    try
    {
        string json = await client.GetStringAsync(url);
        using JsonDocument doc = JsonDocument.Parse(json);
        List<object> results = new();

        foreach (JsonElement quote in doc.RootElement.GetProperty("quotes").EnumerateArray())
        {
            string? symbol = quote.TryGetProperty("symbol", out JsonElement s) ? s.GetString() : null;
            string? longName = quote.TryGetProperty("longname", out JsonElement ln) ? ln.GetString() : null;
            string? exchange = quote.TryGetProperty("exchange", out JsonElement ex) ? ex.GetString() : null;
            string? quoteType = quote.TryGetProperty("quoteType", out JsonElement qt) ? qt.GetString() : null;

            if (symbol is not null)
            {
                results.Add(new { symbol, name = longName ?? symbol, exchange = exchange ?? string.Empty, type = quoteType ?? string.Empty });
            }
        }

        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.Ok(new List<object>());
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
record UpdateOptionTransactionRequest(DateTime Date, string Description, string Symbol, string Isin, decimal Quantity, decimal PremiumAmount, string PremiumCurrency, string Type);
record BackupData(List<Transaction> Transactions, List<AssetTransaction>? AssetTransactions);

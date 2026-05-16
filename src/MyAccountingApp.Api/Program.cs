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

CompositeConversionRepository repo = new CompositeConversionRepository("conversions.json");
CurrencyConverter api = new CurrencyConverter(currencyApiKey);
Currencies source = Currencies.EUR;
CurencyRateService currencyRateService = new CurencyRateService(repo, api, source);

builder.Services.AddSingleton<IConversionRepository>(repo);
builder.Services.AddSingleton<ICurrencyRateService>(currencyRateService);
builder.Services.AddSingleton<ITransactionRepository>(
    new CompositeTransactionRepository("transactions.json"));
builder.Services.AddSingleton<IAgent>(sp =>
{
    ICsvParser csvParser = new InteractiveBrokersCsvParser();
    ILogger<InteractiveBrokersCsvAgent> logger = sp.GetRequiredService<ILogger<InteractiveBrokersCsvAgent>>();
    return new InteractiveBrokersCsvAgent(csvParser, logger);
});
builder.Services.AddSingleton<IMarketPriceService, YahooMarketPriceService>();

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/transactions", (ITransactionRepository repo) =>
{
    IEnumerable<Transaction> transactions = repo.GetAll();
    return Results.Ok(transactions);
});

app.Run();

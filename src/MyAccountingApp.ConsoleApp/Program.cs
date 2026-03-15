using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

CompositeConversionRepository repo = new CompositeConversionRepository("conversions.json");
CurrencyConverter api = new CurrencyConverter();
Currencies source = Currencies.EUR;
CurencyRateService service = new CurencyRateService(repo, api, source);

DateTime targetDate = new DateTime(2024, 12, 1);

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
ILogger<InteractiveBrokersAgent> logger = loggerFactory.CreateLogger<InteractiveBrokersAgent>();
ILogger<OllamaClient> ollamaLogger = loggerFactory.CreateLogger<OllamaClient>();

HttpClient httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(3),
};
IOllamaClient ollamaClient = new OllamaClient(httpClient, ollamaLogger);
IInteractiveBrokersPromptBuilder promptBuilder = new InteractiveBrokersPromptBuilder();

InteractiveBrokersAgent ibAgent = new InteractiveBrokersAgent(
    ollamaClient,
    promptBuilder,
    "llama3",
    logger);

string filePath = "C:/Users/Francesc/source/repos/MyAccountingApp/csv/U8997440_20220523_20221230.csv";

(IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) = await ibAgent.ParseAllAsync(filePath);

Console.WriteLine("--- Transactions ---\n");

if (transactions == null || !transactions.Any())
{
    Console.WriteLine("There are not transactions");
}
else
{
    foreach (Transaction tx in transactions)
    {
        Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Description} | {tx.Money.Amount}{tx.Money.Currency} | {tx.Category}");
    }
}

Console.WriteLine("\n--- Asset Transactions ---\n");

if (assetTransactions == null || !assetTransactions.Any())
{
    Console.WriteLine("There are not asset transactions");
}
else
{
    foreach (AssetTransaction tx in assetTransactions)
    {
        Console.WriteLine($"{tx.Transaction.Date:yyyy-MM-dd} | {tx.Symbol} | {tx.Quantity} | {tx.Transaction.Money.Amount}{tx.Transaction.Money.Currency} | {tx.Type}");
    }
}

YahooMarketPriceService priceService = new YahooMarketPriceService();

Money? applePrice = await priceService.GetPriceAsync("GRF.MC");

if (applePrice == null)
{
    Console.WriteLine("No s'ha pogut obtenir el preu de AAPL");
}
else
{
    Console.WriteLine($"El preu de AAPL is {applePrice!.Amount}{applePrice!.Currency}");
}

Money? dgelPrice = await priceService.GetPriceAsync("DGE.L");

if (dgelPrice == null)
{
    Console.WriteLine("No s'ha pogut obtenir el preu de DGE.L");
}
else
{
    Console.WriteLine($"El preu de AAPL is {dgelPrice!.Amount}{dgelPrice!.Currency}");
}

if (repo.GetByDate(targetDate) == null)
{
    Console.WriteLine("No hi havia conversió per aquesta data. Es farà la crida a l'API...");

    double rate = 1;

    Console.WriteLine($"S'ha guardat la conversió per la data {targetDate:yyyy-MM-dd} amb EUR → USD = {rate}");
}
else
{
    Console.WriteLine($"Conversió ja guardada per la data {targetDate:yyyy-MM-dd}.");
}

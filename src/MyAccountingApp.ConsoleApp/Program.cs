using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

string currencyApiKey = config["CurrencyApi:ApiKey"]
    ?? Environment.GetEnvironmentVariable("CURRENCY_API_KEY")
    ?? throw new InvalidOperationException(
        "CurrencyApi:ApiKey not found. Set it in appsettings.json or the CURRENCY_API_KEY environment variable.");

CompositeConversionRepository repo = new CompositeConversionRepository("conversions.json");
CurrencyConverter api = new CurrencyConverter(currencyApiKey);
Currencies source = Currencies.EUR;
CurencyRateService service = new CurencyRateService(repo, api, source);

DateTime targetDate = new DateTime(2024, 12, 1);

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
ILogger<InteractiveBrokersCsvAgent> logger = loggerFactory.CreateLogger<InteractiveBrokersCsvAgent>();
ILogger<OllamaClient> ollamaLogger = loggerFactory.CreateLogger<OllamaClient>();

HttpClient httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10),
};
IOllamaClient ollamaClient = new OllamaClient(httpClient, ollamaLogger);
ICsvParser csvParser = new InteractiveBrokersCsvParser();

InteractiveBrokersCsvAgent ibAgent = new InteractiveBrokersCsvAgent(
    csvParser,
    ollamaClient,
    "llama3",
    logger);

string[] folderPaths = new string[]
{
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/TRADES",
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/OTHER",
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/CORPORATE",
};

List<Transaction> allTransactions = new List<Transaction>();
List<AssetTransaction> allAssetTransactions = new List<AssetTransaction>();

foreach (string folderPath in folderPaths)
{
    if (!Directory.Exists(folderPath))
    {
        Console.WriteLine($"La carpeta no existeix: {folderPath}");
        continue;
    }

    string[] csvFiles = Directory.GetFiles(folderPath, "*.csv");

    foreach (string csvFile in csvFiles)
    {
        Console.WriteLine($"\n=== Processing: {Path.GetFileName(csvFile)} ===\n");

        if (folderPath.Contains("CORPORATE"))
        {
            IEnumerable<AssetTransaction> corporateAssetTransactions = await ibAgent.ParseCorporateActionsAsync(csvFile);
            allAssetTransactions.AddRange(corporateAssetTransactions);
        }
        else
        {
            (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) = await ibAgent.ParseAllAsync(csvFile);
            allTransactions.AddRange(transactions);
            allAssetTransactions.AddRange(assetTransactions);
        }
    }
}

Console.WriteLine("\n=== ALL TRANSACTIONS ===\n");

Console.WriteLine("--- Transactions ---\n");

if (!allTransactions.Any())
{
    Console.WriteLine("There are no transactions");
}
else
{
    foreach (Transaction tx in allTransactions)
    {
        Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Description} | {tx.Money.Amount}{tx.Money.Currency} | {tx.Category}");
    }
}

Console.WriteLine("\n--- Asset Transactions ---\n");

if (!allAssetTransactions.Any())
{
    Console.WriteLine("There are no asset transactions");
}
else
{
    foreach (AssetTransaction tx in allAssetTransactions)
    {
        Console.WriteLine($"{tx.Transaction.Date:yyyy-MM-dd} | {tx.Symbol} | {tx.Quantity} | {tx.Transaction.Money.Amount}{tx.Transaction.Money.Currency} | {tx.Type}");
    }
}

Console.WriteLine($"\nTotal: {allTransactions.Count} transactions, {allAssetTransactions.Count} asset transactions");

YahooMarketPriceService priceService = new YahooMarketPriceService();

Money? grfPrice = await priceService.GetPriceAsync("GRF.MC");

if (grfPrice == null)
{
    Console.WriteLine("No s'ha pogut obtenir el preu de GRF.MC");
}
else
{
    Console.WriteLine($"El preu de GRF.MC is {grfPrice.Amount}{grfPrice.Currency}");
}

Money? dgePrice = await priceService.GetPriceAsync("DGE.L");

if (dgePrice == null)
{
    Console.WriteLine("No s'ha pogut obtenir el preu de DGE.L");
}
else
{
    Console.WriteLine($"El preu de DGE.L is {dgePrice.Amount}{dgePrice.Currency}");
}

if (repo.GetByDate(targetDate) == null)
{
    Console.WriteLine($"No hi havia conversió per {targetDate:yyyy-MM-dd}. Es farà la crida a l'API...");

    Dictionary<string, double> rates = await api.FetchAllRatesAsync(source, targetDate);
    Conversion conversion = new Conversion(targetDate, source);

    foreach (KeyValuePair<string, double> kv in rates)
    {
        string targetCurrencyCode = kv.Key.Substring(3);

        if (Enum.TryParse<Currencies>(targetCurrencyCode, out Currencies currency))
        {
            conversion.AddOrUpdateQuote(currency, kv.Value);
        }
    }

    repo.AddOrUpdate(conversion);

    if (conversion.TryGetQuote(Currencies.USD, out double rate))
    {
        Console.WriteLine($"S'ha guardat la conversió per la data {targetDate:yyyy-MM-dd} amb EUR → USD = {rate}");
    }
}
else
{
    Console.WriteLine($"Conversió ja guardada per la data {targetDate:yyyy-MM-dd}.");
}

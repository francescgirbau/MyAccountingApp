using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Http.Currency;
using MyAccountingApp.Core.Http.Market;
using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

CurrencyApiOptions currencyOptions = config.GetSection("CurrencyApi").Get<CurrencyApiOptions>() ?? new CurrencyApiOptions();

bool useFrankfurter = string.Equals(currencyOptions.Provider, "Frankfurter", StringComparison.OrdinalIgnoreCase);

CompositeConversionRepository repo = new CompositeConversionRepository("conversions.json");

HttpClient frankfurterClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
HttpClient exchangeRateHostClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

ICurrencyConverter api;
IApiQuotaManager quotaManager;

if (useFrankfurter)
{
    api = new FrankfurterCurrencyConverter(frankfurterClient, currencyOptions.ExcludeCurrencies, currencyOptions.BaseUrl);
    quotaManager = new UnlimitedApiQuotaManager(currencyOptions.ProviderName);
}
else
{
    string currencyApiKey = !string.IsNullOrEmpty(currencyOptions.ApiKey)
        ? currencyOptions.ApiKey
        : config["CurrencyApi:ApiKey"]
            ?? Environment.GetEnvironmentVariable("CURRENCY_API_KEY")
            ?? throw new InvalidOperationException(
                "CurrencyApi:ApiKey not found. Set it in appsettings.json or the CURRENCY_API_KEY environment variable.");

    api = new CurrencyConverter(currencyApiKey, exchangeRateHostClient, currencyOptions.ExcludeCurrencies);
    JsonApiQuotaRepository quotaRepo = new JsonApiQuotaRepository("api_quota.json", currencyOptions.RequestsLimit, currencyOptions.SafetyMargin, currencyOptions.ProviderName);
    quotaManager = new ApiQuotaManager(quotaRepo);
}

Currencies source = Currencies.EUR;
JsonPendingConversionRepository pendingRepo = new JsonPendingConversionRepository("pending_conversions.json");
PendingConversionQueue pendingQueue = new PendingConversionQueue(pendingRepo);

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
CurrencyRateService service = new CurrencyRateService(
    repo,
    api,
    source,
    quotaManager,
    pendingQueue,
    currencyOptions.MaxTimeseriesDays,
    currencyOptions.ProviderName,
    loggerFactory.CreateLogger<CurrencyRateService>());

DateTime targetDate = new DateTime(2024, 12, 1);

ILogger<InteractiveBrokersImportService> logger = loggerFactory.CreateLogger<InteractiveBrokersImportService>();

ICsvParser csvParser = new InteractiveBrokersCsvParser();

InteractiveBrokersImportService ibAgent = new InteractiveBrokersImportService(
    csvParser,
    logger);

string[] folderPaths = new string[]
{
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/TRADES",
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/OTHER",
    "C:/Users/Francesc/source/repos/MyAccountingApp/csv/IBKR/CORPORATE",
};

List<Transaction> allTransactions = new List<Transaction>();
List<AssetTransaction> allAssetTransactions = new List<AssetTransaction>();
List<OptionTransaction> allOptionTransactions = new List<OptionTransaction>();

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
            (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions, IEnumerable<OptionTransaction> optionTransactions) = await ibAgent.ParseAllAsync(csvFile);
            allTransactions.AddRange(transactions);
            allAssetTransactions.AddRange(assetTransactions);
            allOptionTransactions.AddRange(optionTransactions);
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

Console.WriteLine($"\nTotal: {allTransactions.Count} transactions, {allAssetTransactions.Count} asset transactions, {allOptionTransactions.Count} option transactions");

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

    Dictionary<string, decimal> rates = await api.FetchAllRatesAsync(source, targetDate);
    Conversion conversion = new Conversion(targetDate, source);

    foreach (KeyValuePair<string, decimal> kv in rates)
    {
        string targetCurrencyCode = kv.Key.Substring(3);

        if (Enum.TryParse<Currencies>(targetCurrencyCode, out Currencies currency))
        {
            conversion.AddOrUpdateQuote(currency, kv.Value);
        }
    }

    repo.AddOrUpdate(conversion);

    if (conversion.TryGetQuote(Currencies.USD, out decimal rate))
    {
        Console.WriteLine($"S'ha guardat la conversió per la data {targetDate:yyyy-MM-dd} amb EUR → USD = {rate}");
    }
}
else
{
    Console.WriteLine($"Conversió ja guardada per la data {targetDate:yyyy-MM-dd}.");
}

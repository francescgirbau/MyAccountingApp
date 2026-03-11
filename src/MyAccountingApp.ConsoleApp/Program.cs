using MyAccountingApp.Application.Services;
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

InteractiveBrokersAgent ibAgent = new InteractiveBrokersAgent();

string filePath = "C:/Users/Francesc/source/repos/MyAccountingApp/csv/U8997440_20220523_20221230.csv";

IEnumerable<Transaction> transactionResult = ibAgent.ParseTransactionsAsync(filePath).Result;

if (transactionResult == null || !transactionResult.Any())
{
    Console.WriteLine("There are not transactions");
}
else
{
    foreach (Transaction tx in transactionResult)
    {
        Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Description} | {tx.Money.Amount}{tx.Money.Currency} | {tx.Category}");
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

    // Fem servir la lògica del servei per obtenir un tipus de canvi, això ja força a guardar tota la conversió
    double rate = 1; // await service.GetExchangeRateAsync(Currencies.USD, targetDate);

    Console.WriteLine($"S'ha guardat la conversió per la data {targetDate:yyyy-MM-dd} amb EUR → USD = {rate}");
}
else
{
    Console.WriteLine($"Conversió ja guardada per la data {targetDate:yyyy-MM-dd}.");
}

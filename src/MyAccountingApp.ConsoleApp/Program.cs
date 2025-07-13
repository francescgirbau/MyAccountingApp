using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Infrastructure.Repositories;
using MyAccountingApp.Infrastructure.Services;

var repo = new JsonConversionRepository("conversions.json");
var api = new CurrencyConverter();
var service = new CurrencyConversionService(repo, api);

DateTime targetDate = new DateTime(2024, 12, 1);

if (!repo.ExistsForDate(targetDate))
{
    Console.WriteLine("No hi havia conversió per aquesta data. Es farà la crida a l'API...");

    // Fem servir la lògica del servei per obtenir un tipus de canvi, això ja força a guardar tota la conversió
    double rate = await service.GetExchangeRateAsync(Currencies.USD, targetDate);

    Console.WriteLine($"S'ha guardat la conversió per la data {targetDate:yyyy-MM-dd} amb EUR → USD = {rate}");
}
else
{
    Console.WriteLine($"Conversió ja guardada per la data {targetDate:yyyy-MM-dd}.");
}

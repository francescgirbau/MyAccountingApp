using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Enums;

CompositeConversionRepository repo = new CompositeConversionRepository("conversions.json");
CurrencyConverter api = new CurrencyConverter();
Currencies source = Currencies.EUR;
CurencyRateService service = new CurencyRateService(repo, api, source);

DateTime targetDate = new DateTime(2024, 12, 1);

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

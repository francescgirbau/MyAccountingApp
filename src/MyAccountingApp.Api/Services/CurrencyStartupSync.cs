using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using Serilog;

namespace MyAccountingApp.Api.Services;

public sealed class CurrencyStartupSync : BackgroundService
{
    private readonly ICurrencyRateService _currencyRateService;
    private readonly CurrencyApiOptions _options;

    public CurrencyStartupSync(ICurrencyRateService currencyRateService, CurrencyApiOptions options)
    {
        this._currencyRateService = currencyRateService;
        this._options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!await this._currencyRateService.BackfillIfEmptyAsync(this._options.BackfillDaysOnFirstRun))
            {
                await this._currencyRateService.SyncGapAsync(this._options.MaxTimeseriesDays);
            }

            await this._currencyRateService.ProcessPendingAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Currency API startup sync failed");
        }
    }
}

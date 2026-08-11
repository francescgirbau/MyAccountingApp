using Microsoft.Extensions.Configuration;
using MyAccountingApp.Api.Http;
using MyAccountingApp.Api.Services;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Options;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Http.Currency;
using MyAccountingApp.Core.Http.Market;
using MyAccountingApp.Core.Imports.AbnAmro;
using MyAccountingApp.Core.Imports.Cobas;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Core.Imports.Degiro;
using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Core.Imports.MyInvestor;
using MyAccountingApp.Core.Imports.Revolut;
using MyAccountingApp.Core.Imports.SelfBank;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using Serilog;

namespace MyAccountingApp.Api;

public static class DependencyInjection
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
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

        CurrencyApiOptions currencyOptions = builder.Configuration.GetSection("CurrencyApi").Get<CurrencyApiOptions>() ?? new CurrencyApiOptions();

        bool useFrankfurter = string.Equals(currencyOptions.Provider, "Frankfurter", StringComparison.OrdinalIgnoreCase);

        CompositeConversionRepository repo = new CompositeConversionRepository("data/conversions.json");

        builder.Services.AddHttpClient("Frankfurter", client =>
        {
            client.BaseAddress = new Uri(currencyOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<FxRetryHandler>();
        builder.Services.AddHttpClient("ExchangeRateHost", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<FxRetryHandler>();
        builder.Services.AddTransient<FxRetryHandler>();

        IApiQuotaManager quotaManager;
        JsonApiQuotaRepository? quotaRepo = null;

        if (useFrankfurter)
        {
            quotaManager = new UnlimitedApiQuotaManager(currencyOptions.ProviderName);
        }
        else
        {
            string currencyApiKey = !string.IsNullOrEmpty(currencyOptions.ApiKey)
                ? currencyOptions.ApiKey
                : builder.Configuration["CurrencyApi:ApiKey"]
                    ?? Environment.GetEnvironmentVariable("CURRENCY_API_KEY")
                    ?? throw new InvalidOperationException(
                        "CurrencyApi:ApiKey not found. Set it in appsettings.json or the CURRENCY_API_KEY environment variable.");

            quotaRepo = new JsonApiQuotaRepository("data/api_quota.json", currencyOptions.RequestsLimit, currencyOptions.SafetyMargin, currencyOptions.ProviderName);
            quotaManager = new ApiQuotaManager(quotaRepo);
        }

        Currencies source = Currencies.EUR;
        JsonPendingConversionRepository pendingRepo = new JsonPendingConversionRepository("data/pending_conversions.json");
        PendingConversionQueue pendingQueue = new PendingConversionQueue(pendingRepo);

        builder.Services.AddSingleton<ICurrencyConverter>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            if (useFrankfurter)
            {
                return new FrankfurterCurrencyConverter(httpClientFactory.CreateClient("Frankfurter"), currencyOptions.ExcludeCurrencies, currencyOptions.BaseUrl);
            }

            string currencyApiKey = !string.IsNullOrEmpty(currencyOptions.ApiKey)
                ? currencyOptions.ApiKey
                : builder.Configuration["CurrencyApi:ApiKey"]
                    ?? Environment.GetEnvironmentVariable("CURRENCY_API_KEY")
                    ?? throw new InvalidOperationException(
                        "CurrencyApi:ApiKey not found. Set it in appsettings.json or the CURRENCY_API_KEY environment variable.");

            return new CurrencyConverter(currencyApiKey, httpClientFactory.CreateClient("ExchangeRateHost"), currencyOptions.ExcludeCurrencies);
        });

        builder.Services.AddSingleton<IConversionRepository>(repo);
        if (quotaRepo != null)
        {
            builder.Services.AddSingleton<IApiQuotaRepository>(quotaRepo);
        }

        builder.Services.AddSingleton<IPendingConversionRepository>(pendingRepo);
        builder.Services.AddSingleton<IApiQuotaManager>(quotaManager);
        builder.Services.AddSingleton<IPendingConversionQueue>(pendingQueue);
        builder.Services.AddSingleton<ICurrencyRateService>(sp =>
        {
            ICurrencyConverter api = sp.GetRequiredService<ICurrencyConverter>();
            IApiQuotaManager quota = sp.GetRequiredService<IApiQuotaManager>();
            IPendingConversionQueue queue = sp.GetRequiredService<IPendingConversionQueue>();
            return new CurrencyRateService(
                repo,
                api,
                source,
                quota,
                queue,
                currencyOptions.MaxTimeseriesDays,
                currencyOptions.ProviderName,
                sp.GetRequiredService<ILogger<CurrencyRateService>>());
        });
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
        builder.Services.AddSingleton<RevolutImportService>();
        builder.Services.AddSingleton<AbnAmroImportService>();
        builder.Services.AddSingleton<CobasImportService>();
        builder.Services.AddSingleton<MyInvestorAccountImportService>();
        builder.Services.AddSingleton<MyInvestorFundImportService>();
        builder.Services.AddSingleton<SelfBankAccountImportService>();
        builder.Services.AddSingleton<SelfBankFundImportService>();
        builder.Services.AddSingleton<IBrokerImportService, BrokerImportDispatcher>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IMarketPriceService, YahooMarketPriceService>();
        builder.Services.AddSingleton<IImportService, ImportService>();
        builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
        builder.Services.AddSingleton<IAssetTransactionCommandService, AssetTransactionCommandService>();
        builder.Services.AddSingleton<IOptionTransactionCommandService, OptionTransactionCommandService>();
        builder.Services.AddSingleton<ITransactionCommandService, TransactionCommandService>();
        builder.Services.AddSingleton<IPortfolioQuery, PortfolioQuery>();
        builder.Services.AddSingleton<IPositionEngine, PositionEngine>();
        builder.Services.AddSingleton<IValidationQuery, ValidationQuery>();
        builder.Services.AddSingleton<IAnnualSummaryService, AnnualSummaryService>();
        builder.Services.AddSingleton(currencyOptions);
        builder.Services.AddHostedService<CurrencyStartupSync>();

        builder.Host.UseSerilog();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MyAccountingApp.Api.Tests.Fakes;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Api.Tests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<ICurrencyRateService>();
            services.AddSingleton<ICurrencyRateService>(new FakeCurrencyRateService());
            services.RemoveAll<IConversionRepository>();
            services.AddSingleton<IConversionRepository>(new InMemoryConversionRepository());
            services.RemoveAll<ITransactionRepository>();
            services.AddSingleton<ITransactionRepository>(new InMemoryTransactionRepository());
            services.RemoveAll<IPortfolioRepository>();
            services.AddSingleton<IPortfolioRepository>(new InMemoryPortfolioRepository());
            services.RemoveAll<IOptionTransactionRepository>();
            services.AddSingleton<IOptionTransactionRepository>(new JsonOptionTransactionRepository(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")));
            services.RemoveAll<IPendingConversionQueue>();
            services.AddSingleton<IPendingConversionQueue>(new FakePendingConversionQueue());
        });
    }
}

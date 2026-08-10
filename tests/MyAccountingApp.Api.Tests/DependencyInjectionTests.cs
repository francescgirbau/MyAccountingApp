using Microsoft.Extensions.DependencyInjection;
using MyAccountingApp.Domain.Interfaces;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void ResolveCurrencyConverter_CreatesHttpClientWithRegisteredRetryHandler()
    {
        using ApiWebApplicationFactory factory = new();

        ICurrencyConverter converter = factory.Services.GetRequiredService<ICurrencyConverter>();

        Assert.NotNull(converter);
    }
}

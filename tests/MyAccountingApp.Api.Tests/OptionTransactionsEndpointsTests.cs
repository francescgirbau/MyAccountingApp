using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Tests;

public class OptionTransactionsEndpointsTests
{
    private static object CreateOptionTransactionBody(DateTime date, string type = "Buy", decimal quantity = 2m)
    {
        return new
        {
            date,
            description = "Test option",
            amount = 100m,
            currency = "EUR",
            category = "EXPENSE",
            symbol = "AAPL",
            isin = "US0378331005",
            quantity,
            type,
        };
    }

    private static void SeedOptionTransaction(ApiWebApplicationFactory factory, Guid id, DateTime date)
    {
        Transaction transaction = new(id, date, "Seed option", new Money(100m, "EUR"), TransactionCategory.EXPENSE);
        IOptionTransactionRepository repository = factory.Services.GetRequiredService<IOptionTransactionRepository>();
        repository.Initialize(new[] { new OptionTransaction(transaction, "AAPL", "US0378331005", 2m, AssetTransactionType.Buy) });
    }

    [Fact]
    public async Task OptionTransactions_ShouldUpdateAndDelete()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        DateTime date = new(2026, 8, 1, 10, 0, 0);
        Guid id = Guid.NewGuid();
        SeedOptionTransaction(factory, id, date);

        // Act
        HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/option-transactions/{id}", CreateOptionTransactionBody(date));

        // Assert
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        HttpResponseMessage list = await client.GetAsync("/api/option-transactions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(listDocument.RootElement.EnumerateArray());

        HttpResponseMessage bySymbol = await client.GetAsync("/api/option-transactions/AAPL");
        Assert.Equal(HttpStatusCode.OK, bySymbol.StatusCode);
        using JsonDocument bySymbolDocument = JsonDocument.Parse(await bySymbol.Content.ReadAsStringAsync());
        Assert.Single(bySymbolDocument.RootElement.EnumerateArray());

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/option-transactions/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task OptionTransactions_ShouldReturnNotFound_WhenUpdatingMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/option-transactions/{Guid.NewGuid()}", CreateOptionTransactionBody(new DateTime(2026, 8, 1)));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OptionTransactions_YearDelete_ShouldReturnCount()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        Guid id = Guid.NewGuid();
        SeedOptionTransaction(factory, id, new DateTime(2026, 8, 1));

        // Act
        HttpResponseMessage response = await client.DeleteAsync("/api/option-transactions/year/2026");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("deletedOptions").GetInt32());
    }
}

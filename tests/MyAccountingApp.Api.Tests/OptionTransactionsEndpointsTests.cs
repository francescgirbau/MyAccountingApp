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

    [Fact]
    public async Task BatchPatch_ShouldUpdateSymbols_ForAllIds()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Transaction tx1 = new(first, new DateTime(2026, 8, 1), "Seed option", new Money(100m, "EUR"), TransactionCategory.EXPENSE);
        Transaction tx2 = new(second, new DateTime(2026, 8, 2), "Seed option", new Money(100m, "EUR"), TransactionCategory.EXPENSE);
        IOptionTransactionRepository repository = factory.Services.GetRequiredService<IOptionTransactionRepository>();
        repository.Initialize(new[]
        {
            new OptionTransaction(tx1, "AAPL", "US0378331005", 2m, AssetTransactionType.Buy),
            new OptionTransaction(tx2, "AAPL", "US0378331005", 2m, AssetTransactionType.Buy),
        });

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/option-transactions/batch",
            new { ids = new[] { first, second }, patch = new { symbol = "MSFT 260918C00330000" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("updated").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("failures").EnumerateArray());

        HttpResponseMessage all = await client.GetAsync("/api/option-transactions");
        using JsonDocument allDocument = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        Assert.All(allDocument.RootElement.EnumerateArray(), tx => Assert.Equal("MSFT 260918C00330000", tx.GetProperty("symbol").GetString()));
    }

    [Fact]
    public async Task BatchPatch_ShouldReportMissingId_AsFailure()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        Guid existing = Guid.NewGuid();
        Guid missing = Guid.NewGuid();
        SeedOptionTransaction(factory, existing, new DateTime(2026, 8, 1));

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/option-transactions/batch",
            new { ids = new[] { existing, missing }, patch = new { symbol = "TSLA" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("updated").GetInt32());
        JsonElement failure = Assert.Single(document.RootElement.GetProperty("failures").EnumerateArray());
        Assert.Equal(missing, failure.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BatchPatch_EmptyIds_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/option-transactions/batch",
            new { ids = Array.Empty<Guid>(), patch = new { symbol = "TSLA" } });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Tests;

public class IBKRDataHealthEndpointsTests
{
    [Fact]
    public async Task DataHealth_ReturnsReportWithSeededData()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        ITransactionRepository txRepo = factory.Services.GetRequiredService<ITransactionRepository>();
        txRepo.Initialize(new[]
        {
            new Transaction(new DateTime(2024, 5, 1), "deposit", new Money(1000m, "EUR"), TransactionCategory.DEPOSIT, "U8997440_2024_2024.csv"),
        });

        IPortfolioRepository portfolioRepo = factory.Services.GetRequiredService<IPortfolioRepository>();
        AssetTransaction asset = new(
            new Transaction(new DateTime(2024, 6, 1), "X", new Money(10m, "EUR"), TransactionCategory.DIVIDEND),
            "X",
            1m,
            AssetTransactionType.Buy);
        asset.SetSource("U8997440_2024_2024.csv");
        portfolioRepo.Initialize(new[] { asset });

        IOptionTransactionRepository optionRepo = factory.Services.GetRequiredService<IOptionTransactionRepository>();
        optionRepo.Initialize(new[]
        {
            new OptionTransaction(
                new Transaction(new DateTime(2024, 5, 22), "EC 16MAY25 10 P", new Money(300m, "EUR"), TransactionCategory.DIVESTMENT),
                "EC",
                "IE0000000000",
                1m,
                AssetTransactionType.Sell),
        });

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/ibkr/data-health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IBKRDataHealthDto? report = await response.Content.ReadFromJsonAsync<IBKRDataHealthDto>();
        Assert.NotNull(report);
        Assert.Equal(1, report!.TotalTransactions);
        Assert.Equal(1, report.IbkrTransactions);
        Assert.Equal(1, report.TotalAssets);
        Assert.Equal(1, report.IbkrAssets);
        Assert.Equal(1, report.TotalOptions);
        IBKROpenExpiredContractDto expired = Assert.Single(report.OpenExpiredContracts);
        Assert.Equal("EC 16MAY25 10 P", expired.Description);
        IBKRPhantomAssetDto phantom = Assert.Single(report.PhantomAssets);
        Assert.Equal("X", phantom.Symbol);
    }
}
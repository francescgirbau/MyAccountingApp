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

    [Fact]
    public async Task RebuildPreview_ReturnsCountsForRequestedYears()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        ITransactionRepository txRepo = factory.Services.GetRequiredService<ITransactionRepository>();
        txRepo.Initialize(new[]
        {
            new Transaction(new DateTime(2024, 5, 1), "deposit", new Money(1000m, "EUR"), TransactionCategory.DEPOSIT, "U8997440_2024_2024.csv"),
            new Transaction(new DateTime(2024, 5, 2), "manual", new Money(10m, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(new DateTime(2025, 1, 1), "dividend", new Money(5m, "EUR"), TransactionCategory.DIVIDEND, "U8997440_2025_2025.csv"),
        });

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/ibkr/rebuild/preview", new { years = new[] { 2024 } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IBKRRebuildPreviewDto? preview = await response.Content.ReadFromJsonAsync<IBKRRebuildPreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.Transactions);
        Assert.Equal(0, preview.Assets);
        Assert.Equal(0, preview.Options);

        IBKRRebuildYearDto year = Assert.Single(preview.Years);
        Assert.Equal(2024, year.Year);
        Assert.Equal(1, year.Transactions);
    }

    [Fact]
    public async Task Rebuild_DeletesIbkrAndOrphanRowsAndCreatesBackup()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        ITransactionRepository txRepo = factory.Services.GetRequiredService<ITransactionRepository>();
        txRepo.Initialize(new[]
        {
            new Transaction(new DateTime(2024, 5, 1), "deposit", new Money(1000m, "EUR"), TransactionCategory.DEPOSIT, "U8997440_2024_2024.csv"),
            new Transaction(new DateTime(2024, 5, 2), "manual", new Money(10m, "EUR"), TransactionCategory.EXPENSE),
        });

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
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/ibkr/rebuild", new { years = new[] { 2024 } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IBKRRebuildResultDto? result = await response.Content.ReadFromJsonAsync<IBKRRebuildResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result!.TransactionsDeleted);
        Assert.Equal(0, result.AssetsDeleted);
        Assert.Equal(1, result.OptionsDeleted);
        Assert.Equal(1, result.OptionsWithoutSourceDeleted);
        Assert.False(string.IsNullOrWhiteSpace(result.BackupFile));
        Assert.True(File.Exists(result.BackupFile));

        Assert.Single(txRepo.GetAll());
        Assert.Equal("manual", txRepo.GetAll().Single().Description);
        Assert.Empty(optionRepo.GetAll());

        if (result.BackupFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            string backupJson = await File.ReadAllTextAsync(result.BackupFile);
            Assert.Contains("deposit", backupJson);
        }
    }

    [Fact]
    public async Task Rebuild_WithoutYears_ReturnsBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/ibkr/rebuild", new { years = Array.Empty<int>() });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
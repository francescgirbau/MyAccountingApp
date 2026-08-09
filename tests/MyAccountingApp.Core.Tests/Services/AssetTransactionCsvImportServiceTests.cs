namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Common;
using Xunit;

public class AssetTransactionCsvImportServiceTests
{
    [Fact]
    public async Task ParseAllAsync_ParsesAssetTransactions()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n" +
                     "2023-01-15,Compra Fons,ES0123456789,-10000,EUR,MYINVESTOR\n" +
                     "2023-06-10,Venda Fons,ES0123456789,5000,EUR,MYINVESTOR";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (transactions, assetTransactions, _) = await service.ParseAllAsync(file);

            Assert.Empty(transactions);
            Assert.Equal(2, assetTransactions.Count());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_MapsBuyCorrectly()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n" +
                     "2023-03-01,Suscripcion Cobas,COBAS_SELECCION,-2500,EUR,COBAS";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (_, assetTransactions, _) = await service.ParseAllAsync(file);

            var atx = Assert.Single(assetTransactions);
            Assert.Equal("COBAS_SELECCION", atx.Symbol);
            Assert.Equal(1, atx.Quantity);
            Assert.Equal(Domain.Enums.AssetTransactionType.Buy, atx.Type);
            Assert.Equal(2500m, atx.Transaction.Money.Amount);
            Assert.Equal("EUR", atx.Transaction.Money.Currency);
            Assert.Equal(Domain.Enums.TransactionCategory.EXPENSE, atx.Transaction.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_MapsSellCorrectly()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n" +
                     "2023-09-01,Reembolso Fons,ES9876543210,1200,EUR,SELFBANK";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (_, assetTransactions, _) = await service.ParseAllAsync(file);

            var atx = Assert.Single(assetTransactions);
            Assert.Equal("ES9876543210", atx.Symbol);
            Assert.Equal(1, atx.Quantity);
            Assert.Equal(Domain.Enums.AssetTransactionType.Sell, atx.Type);
            Assert.Equal(1200m, atx.Transaction.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.INCOME, atx.Transaction.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsEmptyForHeaderOnly()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (transactions, assetTransactions, _) = await service.ParseAllAsync(file);

            Assert.Empty(transactions);
            Assert.Empty(assetTransactions);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_DetectsTransferByKeyword()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n" +
                     "2023-04-01,Traspaso a FRANCESC,ES0123456789,-5000,EUR,MYINVESTOR";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (_, assetTransactions, _) = await service.ParseAllAsync(file);

            var atx = Assert.Single(assetTransactions);
            Assert.Equal(Domain.Enums.TransactionCategory.TRANSFER, atx.Transaction.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_IgnoresBadLines()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n" +
                     "bad-date,desc,TICK,abc,EUR,SRC\n" +
                     "2023-01-01,good,TICK,-100,EUR,SRC";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            AssetTransactionCsvImportService service = new();
            var (_, assetTransactions, _) = await service.ParseAllAsync(file);

            var atx = Assert.Single(assetTransactions);
            Assert.Equal(100m, atx.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty()
    {
        AssetTransactionCsvImportService service = new();
        var result = await service.ParseCorporateActionsAsync("dummy.csv");

        Assert.Empty(result);
    }
}

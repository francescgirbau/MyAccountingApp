namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.MyInvestor;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

public class MyInvestorFundImportServiceTests
{
    private const string Header = "Fecha de la orden;ISIN;Importe estimado;Nº de participaciones;Estado";

    [Fact]
    public async Task ParseAllAsync_Finalizada_CreatesBuyAssetTransaction()
    {
        string csv = $"{Header}\n22/12/2023;ES0165243017;250 EUR;234,913;Finalizada";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorFundImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(options);
            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Buy, asset.Type);
            Assert.Equal(TransactionCategory.INVESTMENT, asset.Transaction.Category);
            Assert.Equal("ES0165243017", asset.Symbol);
            Assert.Equal(234.913m, asset.Quantity);
            Assert.Equal(250.00m, asset.Transaction.Money.Amount);
            Assert.Equal("EUR", asset.Transaction.Money.Currency);
            Assert.Equal(new DateTime(2023, 12, 22), asset.Transaction.Date);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_NotFinalizada_Skipped()
    {
        string csv = $"{Header}\n22/12/2023;ES0165243017;250 EUR;234,913;Anulada";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_EmptyIsin_Skipped()
    {
        string csv = $"{Header}\n22/12/2023;;250 EUR;234,913;Finalizada";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ZeroQuantity_Skipped()
    {
        string csv = $"{Header}\n22/12/2023;ES0165243017;250 EUR;0;Finalizada";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

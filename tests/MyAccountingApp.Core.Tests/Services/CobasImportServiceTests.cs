namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Cobas;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

public class CobasImportServiceTests
{
    private const string Header = "Operacion,Producto,Fecha,Tipo,Estado,Importe,Valor liquidativo,Participaciones";

    [Fact]
    public async Task ParseAllAsync_Suscripcion_CreatesBuyAssetTransaction()
    {
        string csv = $"{Header}\nO-BEC1579,Cobas Internacional FI Clase D,11/11/2025,Suscripción,Finalizada,120,00 € (Bruto),238.733905€,0.502652";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(options);
            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Buy, asset.Type);
            Assert.Equal(TransactionCategory.INVESTMENT, asset.Transaction.Category);
            Assert.Equal("COBAS_INTERNACIONAL_D", asset.Symbol);
            Assert.Equal(0.502652m, asset.Quantity);
            Assert.Equal(120.00m, asset.Transaction.Money.Amount);
            Assert.Equal("EUR", asset.Transaction.Money.Currency);
            Assert.Equal(new DateTime(2025, 11, 11), asset.Transaction.Date);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TraspasoDeSalida_CreatesSellAssetTransaction()
    {
        string csv = $"{Header}\nO-BEG2466,Cobas Internacional FI Clase D,23/02/2026,Traspaso de salida,Finalizada,2.560,32 € (Bruto),291.910724€,8.770893";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Sell, asset.Type);
            Assert.Equal(TransactionCategory.INCOME, asset.Transaction.Category);
            Assert.Equal(8.770893m, asset.Quantity);
            Assert.Equal(2560.32m, asset.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TraspasoDeEntrada_CreatesBuyAssetTransaction()
    {
        string csv = $"{Header}\nO-BEG2447,Cobas Internacional FI Clase C,23/02/2026,Traspaso de entrada,Finalizada,2.560,32 € (Bruto),189.351849€,13.521495";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Buy, asset.Type);
            Assert.Equal("COBAS_INTERNACIONAL_C", asset.Symbol);
            Assert.Equal(13.521495m, asset.Quantity);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_AnuladaRow_Skipped()
    {
        string csv = $"{Header}\nO-BAI0957,Cobas Selección FI Clase D,21/02/2023,Suscripción,Anulada,2.500,00 € (Bruto),,\nO-BAI1565,Cobas Selección FI Clase D,21/02/2023,Suscripción,Finalizada,2.500,00 € (Bruto),156.965535€,15.927063";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal("COBAS_SELECCION_D", asset.Symbol);
            Assert.Equal(15.927063m, asset.Quantity);
            Assert.Equal(2500.00m, asset.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_EmptyParticipaciones_Skipped()
    {
        string csv = $"{Header}\nO-X,Cobas Selección FI Clase D,01/01/2024,Suscripción,Finalizada,100,00 € (Bruto),150.5€,";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_UnknownTipo_Skipped()
    {
        string csv = $"{Header}\nO-X,Cobas Selección FI Clase D,01/01/2024,Compra,Finalizada,100,00 € (Bruto),150.5€,0.66";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Symbol_NormalizedWithoutDiacritics()
    {
        string csv = $"{Header}\nO-BAL7805,Cobas Selección FI Clase C,12/12/2023,Suscripción,Finalizada,250,00 € (Bruto),153.864741€,1.624804";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Equal("COBAS_SELECCION_C", Assert.Single(assets).Symbol);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Reembolso_CreatesSellAssetTransaction()
    {
        string csv = $"{Header}\nO-X,Cobas Internacional FI Clase C,01/01/2024,Reembolso,Finalizada,500,00 € (Bruto),200.5€,2.493765";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            CobasImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Sell, asset.Type);
            Assert.Equal(2.493765m, asset.Quantity);
            Assert.Equal(500.00m, asset.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.SelfBank;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

public class SelfBankFundImportServiceTests
{
    private const string Preamble = "Nombre cliente:;FRANCESC GIRBAU LLISTUELLA\nIBAN:;ES4914900001182442902140\nN. de cuenta:;2442902140\nDivisa:;EUR\n";
    private const string Header = "Fecha movimiento;Fecha valor;Movimiento;Valor;Cantidad;Precio;Importe Bruto;Comisión;Canon;Impuestos;Importe total;Plusvalía/Minusvalía;Saldo";

    [Fact]
    public async Task ParseAllAsync_Suscripcion_CreatesBuyAssetTransaction()
    {
        string csv = $"{Preamble}{Header}\n05/09/2025;04/09/2025;Suscripción fondo;Sigma Internacional A FI;6,63081400;18,09732700;-120,00000000;0,00000000;0,00000000;0,00000000;-120,00000000;;120,00000000;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankFundImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(options);
            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Buy, asset.Type);
            Assert.Equal(TransactionCategory.INVESTMENT, asset.Transaction.Category);
            Assert.Equal("SIGMA_INTERNACIONAL_A", asset.Symbol);
            Assert.Equal(6.63081400m, asset.Quantity);
            Assert.Equal(120.00m, asset.Transaction.Money.Amount);
            Assert.Equal("EUR", asset.Transaction.Money.Currency);
            Assert.Equal(new DateTime(2025, 9, 5), asset.Transaction.Date);
            Assert.Equal("Sigma Internacional A FI", asset.Transaction.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_SuscripcionTraspaso_CreatesBuyAssetTransaction()
    {
        string csv = $"{Header}\n16/02/2023;14/02/2023;Suscripción traspaso de fondo;Sigma Internacional A FI;448,55815400;13,10019660;-5876,20000000;0,00000000;0,00000000;0,00000000;0,00000000;;0,00000000;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Buy, asset.Type);
            Assert.Equal(448.55815400m, asset.Quantity);
            Assert.Equal(5876.20m, asset.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TraspasoDeEfectivo_Skipped()
    {
        string csv = $"{Header}\n04/09/2025;04/09/2025;Traspaso de efectivo de entrada;;0,00000000;0,00000000;120,00000000;0,00000000;0,00000000;0,00000000;120,00000000;;240,00000000;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Reembolso_CreatesSellAssetTransaction()
    {
        string csv = $"{Header}\n01/06/2025;31/05/2025;Reembolso fondo;Sigma Internacional A FI;3,00000000;20,00000000;60,00000000;0,00000000;0,00000000;0,00000000;60,00000000;5,00000000;180,00000000;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            AssetTransaction asset = Assert.Single(assets);
            Assert.Equal(AssetTransactionType.Sell, asset.Type);
            Assert.Equal(TransactionCategory.INCOME, asset.Transaction.Category);
            Assert.Equal(3.00000000m, asset.Quantity);
            Assert.Equal(60.00m, asset.Transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ZeroQuantity_Skipped()
    {
        string csv = $"{Header}\n04/09/2025;04/09/2025;Suscripción fondo;Sigma Internacional A FI;0,00000000;18,09732700;-120,00000000;0,00000000;0,00000000;0,00000000;-120,00000000;;120,00000000;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankFundImportService service = new();

            var (_, assets, _) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

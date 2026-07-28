namespace MyAccountingApp.Core.Tests.Services;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Services;
using Xunit;

public class DegiroTransactionImportServiceTests
{
    private readonly DegiroTransactionImportService service = new();

    [Fact]
    public async Task ParseAllAsync_Buy_CreatesAssetTransaction()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n" +
            "30-12-2021,09:08,GRIFOLS SA CLASS A,ES0171996087,MAD,XMAD,40,\"16,5300\",EUR,\"-661,20\",EUR,\"-661,20\",,\"0,00\",\"-0,50\",\"-661,70\",,577e222d-567c-4f9a-aa66-f462e3508a20";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            var a = Assert.Single(assets);
            Assert.Equal("GRIFOLS", a.Symbol);
            Assert.Equal(40, a.Quantity);
            Assert.Equal(661.20m, a.Transaction.Money.Amount);
            Assert.Equal("EUR", a.Transaction.Money.Currency);
            Assert.Equal(Domain.Enums.AssetTransactionType.Buy, a.Type);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Sell_CreatesAssetTransaction()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n" +
            "15-10-2025,20:41,UNITED NATURAL FOODS INC,US9111631035,NSY,XNAS,21,\"43,0000\",USD,\"903,00\",USD,\"797,32\",\"1,1326\",\"-0,80\",,\"796,52\",,9731a893-b30b-4d04-b5c9-734ed5617a74";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            var a = Assert.Single(assets);
            Assert.Equal("UNITED", a.Symbol);
            Assert.Equal(21, a.Quantity);
            Assert.Equal(903, a.Transaction.Money.Amount);
            Assert.Equal("USD", a.Transaction.Money.Currency);
            Assert.Equal(Domain.Enums.AssetTransactionType.Sell, a.Type);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_BtcetcBuy_ParsesSymbol()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n" +
            "24-10-2025,17:15,BTCETC BITCOIN EXCHANGE TRADED,DE000A27Z304,XETR,XETR,5,\"99,2260\",USD,\"-496,13\",USD,\"-438,07\",\"1,1326\",\"-0,44\",,\"-438,51\",,16e33fba-6d63-411c-bf21-bb8ef4fe3222";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            var a = Assert.Single(assets);
            Assert.Equal("BTCETC", a.Symbol);
            Assert.Equal(5, a.Quantity);
            Assert.Equal(496.13m, a.Transaction.Money.Amount);
            Assert.Equal("USD", a.Transaction.Money.Currency);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_AdrSymbol_ExtractsCorrectly()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n" +
            "08-11-2021,21:40,ADR ON HIMAX TECHNOLOGIES INC,US43289P1066,NDQ,XNAS,40,\"10,9500\",USD,\"-438,00\",USD,\"-377,94\",\"1,1589\",\"-0,38\",\"-0,64\",\"-378,96\",,295729ae-38ed-47b5-b5f6-199e3315e898";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            var a = Assert.Single(assets);
            Assert.Equal("HIMAX", a.Symbol);
            Assert.Equal(40, a.Quantity);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_EmptyFile_ReturnsEmpty()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_SellWithNegativeQuantity_CreatesAssetTransaction()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n" +
            "01-03-2023,09:01,REE P15.50 17MAR23,ES0A03271814,MEF,XMRV,-1,\"0,1500\",EUR,\"15,00\",EUR,\"15,00\",,\"0,00\",\"-0,75\",\"14,25\",,0eb322a1-5668-4e28-bd12-12fcdb1bd25d";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            var a = Assert.Single(assets);
            Assert.Equal("REE", a.Symbol);
            Assert.Equal(1, a.Quantity);
            Assert.Equal(15, a.Transaction.Money.Amount);
            Assert.Equal("EUR", a.Transaction.Money.Currency);
            Assert.Equal(Domain.Enums.AssetTransactionType.Sell, a.Type);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

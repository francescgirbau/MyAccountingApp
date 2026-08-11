namespace MyAccountingApp.Core.Tests.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Degiro;
using Xunit;

public class DegiroImportServiceTests
{
    private readonly DegiroImportService service = new();

    [Fact]
    public async Task ParseAllAsync_Buy_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "30-12-2021,09:08,30-12-2021,GRIFOLS SA CLASS A,ES0171996087,\"Compra 40 Grifols SA Class A@16,53 EUR (ES0171996087)\",,EUR,\"-661,20\",EUR,\"1837,81\",577e222d-567c-4f9a-aa66-f462e3508a20";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Sell_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "15-10-2025,20:41,15-10-2025,UNITED NATURAL FOODS INC,US9111631035,\"Venta 21 United Natural Foods Inc@43 USD (US9111631035)\",,USD,\"903,00\",USD,\"11004,26\",9731a893-b30b-4d04-b5c9-734ed5617a74";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Dividend_CreatesTransaction()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "24-12-2025,07:44,23-12-2025,META PLATFORMS INC CLASS A,US30303M1027,Dividendo,,USD,\"6,30\",USD,\"110,85\",\n" +
            "24-12-2025,07:44,23-12-2025,META PLATFORMS INC CLASS A,US30303M1027,\"Retención del dividendo\",,USD,\"-0,95\",USD,\"104,55\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            Assert.Equal(2, txs.Count());
            Assert.Equal("Dividendo", txs.First().Description);
            Assert.Equal(6.30m, txs.First().Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_CashSweep_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "24-12-2025,08:31,24-12-2025,,,Degiro Cash Sweep Transfer,,USD,\"-5,35\",USD,\"110,85\",\n" +
            "24-12-2025,08:31,24-12-2025,,,\"Transferir a su Cuenta de Efectivo en flatexDEGIRO Bank: 5,35 USD\",,,,USD,\"116,20\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_EmptyFile_ReturnsEmpty()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Commission_CreatesFee()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "24-10-2025,17:15,24-10-2025,BTCETC BITCOIN EXCHANGE TRADED,DE000A27Z304,\"Costes de transacción y/o externos de DEGIRO\",,EUR,\"-3,00\",EUR,\"96,06\",16e33fba-6d63-411c-bf21-bb8ef4fe3222";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(3.00m, t.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.FEE, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_BtcetcBuy_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "24-10-2025,17:15,24-10-2025,BTCETC BITCOIN EXCHANGE TRADED,DE000A27Z304,\"Compra 5 BTCetc Bitcoin Exchange Traded Crypto ETN@99,226 USD (DE000A27Z304)\",,USD,\"-496,13\",USD,\"89,61\",16e33fba-6d63-411c-bf21-bb8ef4fe3222";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_FlatexDeposit_CreatesDepositTransaction()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "07-11-2021,10:30,07-11-2021,,,Flatex Deposit,,EUR,\"8000,00\",EUR,\"8000,00\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(8000m, t.Money.Amount);
            Assert.Equal("EUR", t.Money.Currency);
            Assert.Equal(Domain.Enums.TransactionCategory.DEPOSIT, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_FlatexWithdrawal_CreatesTransfer()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "17-12-2021,10:30,17-12-2021,,,Flatex Withdrawal,,EUR,\"-800,00\",EUR,\"7200,00\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(800m, t.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.TRANSFER, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ProcessedFlatexWithdrawal_CreatesTransfer()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "17-12-2021,10:30,17-12-2021,,,Processed Flatex Withdrawal,,EUR,\"800,00\",EUR,\"8000,00\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(800m, t.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.TRANSFER, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_Ingreso_CreatesDepositTransaction()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "01-11-2021,01:06,31-10-2021,,,Ingreso,,EUR,\"50,00\",EUR,\"50,00\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(50m, t.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.DEPOSIT, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_OptionExercise_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "18-03-2023,10:00,17-03-2023,REDEIA CORPORACION SA,ES0173093024,\"OPCIÓN EJERCIDA: Compra 100 Redeia Corporacion SA@15,5 EUR (ES0173093024)\",,EUR,\"-1550,00\",EUR,\"-1462,20\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_CosteDeLaAccion_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "02-04-2024,13:17,02-04-2024,SOLVENTUM CORP,US83444M1018,Coste de la Acción,,USD,\"17,54\",USD,\"82,82\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_SecuritiesLending_CreatesIncome()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n" +
            "01-11-2025,10:00,31-10-2025,,,Ingresos por Préstamo de Valores - Octubre,,EUR,\"5,00\",EUR,\"100,00\",\n";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            var (txs, assets, _) = await this.service.ParseAllAsync(file);

            Assert.Empty(assets);
            var t = Assert.Single(txs);
            Assert.Equal(5m, t.Money.Amount);
            Assert.Equal(Domain.Enums.TransactionCategory.INCOME, t.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

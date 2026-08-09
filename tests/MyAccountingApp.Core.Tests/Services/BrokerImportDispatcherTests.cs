namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.AbnAmro;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Core.Imports.Degiro;
using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Core.Imports.Revolut;
using Xunit;

public class BrokerImportDispatcherTests
{
    private static readonly InteractiveBrokersCsvParser Parser = new();

    [Fact]
    public async Task ParseAllAsync_AssetTransactionSuffix_UsesAssetService()
    {
        string csv = "Data,Descripcio,Ticker,Import,Moneda,Source\n2023-01-15,Compra,ES0123456789,-10000,EUR,SRC";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.NotEmpty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_BankTransactionSuffix_UsesBankService()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n2023-01-15,Nomina,2500,EUR,SRC";
        string file = Path.GetTempFileName() + "_transactions.csv";
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.NotEmpty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_BankCsvHeader_UsesBankService()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n2023-01-15,Nomina,2500,EUR,SRC";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.NotEmpty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_HeaderWithTicker_UsesAssetService()
    {
        string csv = "Data,Ticker,Descripcio,Import,Moneda,Source\n2023-01-15,ES0123456789,Compra,-10000,EUR,SRC";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.NotEmpty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_FallbackToIbkr_WhenNoMatch()
    {
        string csv = "Unknown,Header,Format\n1,2,3";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ThrowsOnNullPath()
    {
        BrokerImportDispatcher dispatcher = CreateDispatcher();

        await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.ParseAllAsync(string.Empty));
    }

    [Fact]
    public async Task ParseAllAsync_DegiroHeader_UsesDegiroService()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n24-12-2025,08:31,24-12-2025,,,Degiro Cash Sweep Transfer,,USD,\"-5,35\",USD,\"110,85\",";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_DegiroAccountBuy_Skipped()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n30-12-2021,09:08,30-12-2021,GRIFOLS SA CLASS A,ES0171996087,\"Compra 40 Grifols SA Class A@16,53 EUR (ES0171996087)\",,EUR,\"-661,20\",EUR,\"1837,81\",577e222d-567c-4f9a-aa66-f462e3508a20";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_DegiroTransactionHeader_UsesDegiroTransactionService()
    {
        string csv = "Fecha,Hora,Producto,ISIN,Bolsa de referencia,Centro de ejecución,Número,Precio,,Valor local,,Valor EUR,Tipo de cambio,Comisión AutoFX,Costes de transacción y/o externos EUR,Total EUR,ID Orden\n30-12-2021,09:08,GRIFOLS SA CLASS A,ES0171996087,MAD,XMAD,40,\"16,5300\",EUR,\"-661,20\",EUR,\"-661,20\",,\"0,00\",\"-0,50\",\"-661,70\",,577e222d-567c-4f9a-aa66-f462e3508a20";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.NotEmpty(assets);
            Assert.Equal("GRIFOLS", assets.First().Symbol);
            Assert.Equal(40, assets.First().Quantity);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_DegiroAccountHeader_StillUsesDegiroService()
    {
        string csv = "Fecha,Hora,Fecha valor,Producto,ISIN,Descripción,Tipo,Variación,,Saldo,,ID Orden\n24-12-2025,08:31,24-12-2025,,,Degiro Cash Sweep Transfer,,USD,\"-5,35\",USD,\"110,85\",";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            BrokerImportDispatcher dispatcher = CreateDispatcher();

            var (txs, assets, _) = await dispatcher.ParseAllAsync(file);

            Assert.Empty(txs);
            Assert.Empty(assets);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_DelegatesToIbkr()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "Fake data\n1,2,3");
            BrokerImportDispatcher dispatcher = CreateDispatcher();
            var result = await dispatcher.ParseCorporateActionsAsync(file);

            Assert.Empty(result);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static BrokerImportDispatcher CreateDispatcher()
    {
        InteractiveBrokersImportService ibkr = new(Parser, new FakeLogger<InteractiveBrokersImportService>());
        IBKRFlexQueryImportService flexQuery = new(Array.Empty<IIBKRStatementAgent>());
        return new BrokerImportDispatcher(ibkr, new BankCsvImportService(), new AssetTransactionCsvImportService(), new DegiroImportService(), new DegiroTransactionImportService(), flexQuery, new RevolutImportService(), new AbnAmroImportService());
    }
}

internal class FakeLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}

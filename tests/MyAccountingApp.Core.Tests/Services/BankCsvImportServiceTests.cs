namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Common;
using Xunit;

public class BankCsvImportServiceTests
{
    [Fact]
    public async Task ParseAllAsync_ParsesSimpleTransactions()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n" +
                     "2015-01-01,Dormity,-142.67,EUR,CAIXA_ENGINYERS\n" +
                     "2015-01-01,Nomina,2500,EUR,CAIXA_ENGINYERS\n" +
                     "2015-01-02,Supermercat,-85.30,EUR,CAIXA_ENGINYERS";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, assetTransactions, _) = await service.ParseAllAsync(file);

            Assert.Equal(3, transactions.Count());
            Assert.Empty(assetTransactions);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_MapsFieldsCorrectly()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n" +
                     "2019-06-15,Compra Amazon,-45.99,USD,REVOLUT";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _, _) = await service.ParseAllAsync(file);

            var tx = Assert.Single(transactions);
            Assert.Equal(new DateTime(2019, 6, 15), tx.Date);
            Assert.Equal("Compra Amazon", tx.Description);
            Assert.Equal(45.99m, tx.Money.Amount);
            Assert.Equal("USD", tx.Money.Currency);
            Assert.Equal(Domain.Enums.TransactionCategory.EXPENSE, tx.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_HandlesCommasInQuotedFields()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n" +
                     "2019-01-14,\"R/ TELEFONICA DE ESPANA, S. A. U.\",-48.01,EUR,CAIXA_ENGINYERS";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _, _) = await service.ParseAllAsync(file);

            var tx = Assert.Single(transactions);
            Assert.Equal("R/ TELEFONICA DE ESPANA, S. A. U.", tx.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_UsesPositiveAmountAsIncome()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n" +
                     "2020-03-01,Nomina,3000,EUR,ABN_AMRO\n" +
                     "2020-03-05,Bonus,500,EUR,ABN_AMRO";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _, _) = await service.ParseAllAsync(file);

            Assert.All(transactions, tx => Assert.Equal(Domain.Enums.TransactionCategory.INCOME, tx.Category));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_DetectsTransferByKeyword()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source\n" +
                     "2020-06-01,Transferencia a FRANCESC,-500,EUR,ABN_AMRO\n" +
                     "2020-06-01,Nomina,3000,EUR,ABN_AMRO";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _, _) = await service.ParseAllAsync(file);

            var list = transactions.ToList();
            Assert.Equal(Domain.Enums.TransactionCategory.TRANSFER, list[0].Category);
            Assert.Equal(Domain.Enums.TransactionCategory.INCOME, list[1].Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsEmptyForHeaderOnly()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _, _) = await service.ParseAllAsync(file);

            Assert.Empty(transactions);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty()
    {
        BankCsvImportService service = new();
        var result = await service.ParseCorporateActionsAsync("dummy.csv");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseCsvLine_ShouldSplitSimpleLine()
    {
        List<string> parts = BankCsvImportService.ParseCsvLine("2024-01-15,Supermercat,-85.30,EUR");

        Assert.Equal(4, parts.Count);
        Assert.Equal("2024-01-15", parts[0]);
        Assert.Equal("Supermercat", parts[1]);
        Assert.Equal("-85.30", parts[2]);
        Assert.Equal("EUR", parts[3]);
    }

    [Fact]
    public void ParseCsvLine_ShouldHandleQuotedFieldWithCommas()
    {
        List<string> parts = BankCsvImportService.ParseCsvLine("2024-01-15,\"R/ TELEFONICA, S. A.\",-48.01,EUR");

        Assert.Equal(4, parts.Count);
        Assert.Equal("R/ TELEFONICA, S. A.", parts[1]);
    }

    [Fact]
    public void ParseCsvLine_ShouldHandleQuotedFieldAtEnd()
    {
        List<string> parts = BankCsvImportService.ParseCsvLine("2024-01-15,Test,100,\"USD\"");

        Assert.Equal(4, parts.Count);
        Assert.Equal("USD", parts[3]);
    }
}

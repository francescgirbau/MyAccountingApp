namespace MyAccountingApp.Core.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using MyAccountingApp.Core.Services;
using Xunit;

public class BankCsvImportServiceTests
{
    [Fact]
    public async Task ParseAllAsync_ParsesSimpleTransactions()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria\n" +
                     "2015-01-01,Dormity,-142.67,EUR,CAIXA_ENGINYERS,EXPENSE\n" +
                     "2015-01-01,Nomina,2500,EUR,CAIXA_ENGINYERS,INCOME\n" +
                     "2015-01-02,Supermercat,-85.30,EUR,CAIXA_ENGINYERS,EXPENSE";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, assetTransactions) = await service.ParseAllAsync(file);

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
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria\n" +
                     "2019-06-15,Compra Amazon,-45.99,USD,REVOLUT,EXPENSE";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _) = await service.ParseAllAsync(file);

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
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria\n" +
                     "2019-01-14,\"R/ TELEFONICA DE ESPANA, S. A. U.\",-48.01,EUR,CAIXA_ENGINYERS,EXPENSE";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _) = await service.ParseAllAsync(file);

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
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria\n" +
                     "2020-03-01,Nomina,3000,EUR,ABN_AMRO,INCOME\n" +
                     "2020-03-05,Bonus,500,EUR,ABN_AMRO,INCOME";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _) = await service.ParseAllAsync(file);

            Assert.All(transactions, tx => Assert.Equal(Domain.Enums.TransactionCategory.INCOME, tx.Category));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_UsesCategoryColumnOverSign()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria\n" +
                     "2020-03-01,Transfer savings,-500,EUR,myInvestor,TRANSFER";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _) = await service.ParseAllAsync(file);

            var tx = Assert.Single(transactions);
            Assert.Equal(Domain.Enums.TransactionCategory.TRANSFER, tx.Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsEmptyForHeaderOnly()
    {
        string csv = "Data,Descripcio,Import,Moneda,Source,Categoria";

        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);

            BankCsvImportService service = new();
            var (transactions, _) = await service.ParseAllAsync(file);

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
}

namespace MyAccountingApp.Core.Tests.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.MyInvestor;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

public class MyInvestorAccountImportServiceTests
{
    private const string Header = "Fecha de operación;Fecha de valor;Concepto;Importe;Divisa";

    [Fact]
    public async Task ParseAllAsync_NegativeAmount_CreatesExpenseTransaction()
    {
        string csv = $"{Header}\n21/12/2023;21/12/2023;PAYPAL EUROPE SARL ET CIE SCA;-12,99;EUR";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorAccountImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
            Assert.Empty(options);
            Transaction transaction = Assert.Single(txs);
            Assert.Equal(TransactionCategory.EXPENSE, transaction.Category);
            Assert.Equal(12.99m, transaction.Money.Amount);
            Assert.Equal("EUR", transaction.Money.Currency);
            Assert.Equal(new DateTime(2023, 12, 21), transaction.Date);
            Assert.Equal("PAYPAL EUROPE SARL ET CIE SCA", transaction.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_PositiveAmount_CreatesIncomeTransaction()
    {
        string csv = $"{Header}\n18/12/2023;18/12/2023;Imposible contratar deposito;10.000;EUR";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Transaction transaction = Assert.Single(txs);
            Assert.Equal(TransactionCategory.INCOME, transaction.Category);
            Assert.Equal(10000.00m, transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TransferDescription_BecomesTransfer()
    {
        string csv = $"{Header}\n15/12/2023;15/12/2023;Transferencia a FRANCESC GIRBAU;-500;EUR";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Equal(TransactionCategory.TRANSFER, Assert.Single(txs).Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ZeroAmount_Skipped()
    {
        string csv = $"{Header}\n15/12/2023;15/12/2023;Movimiento nulo;0;EUR";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_MalformedRow_Skipped()
    {
        string csv = $"{Header}\nEsto es una linea corrupta";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv);
            MyInvestorAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

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

public class SelfBankAccountImportServiceTests
{
    private const string Header = "Fecha Operación;Fecha Valor;Movimiento;Categoría;Importe";

    [Fact]
    public async Task ParseAllAsync_NegativeAmount_CreatesExpenseTransaction()
    {
        string csv = $"{Header}\n2025-11-10;2025-11-10;Compra en supermercado;Sin Categoría;-120.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
            Assert.Empty(options);
            Transaction transaction = Assert.Single(txs);
            Assert.Equal(TransactionCategory.EXPENSE, transaction.Category);
            Assert.Equal(120.00m, transaction.Money.Amount);
            Assert.Equal("EUR", transaction.Money.Currency);
            Assert.Equal(new DateTime(2025, 11, 10), transaction.Date);
            Assert.Equal("Compra en supermercado", transaction.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TrfACobas_BecomesTransfer()
    {
        string csv = $"{Header}\n2025-11-10;2025-11-10;Cargo TRF A COBAS Internacional FI;Sin Categoría;-120.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, assets, options) = await service.ParseAllAsync(file);

            Assert.Empty(assets);
            Assert.Empty(options);
            Transaction transaction = Assert.Single(txs);
            Assert.Equal(TransactionCategory.TRANSFER, transaction.Category);
            Assert.Equal(120.00m, transaction.Money.Amount);
            Assert.Equal("EUR", transaction.Money.Currency);
            Assert.Equal(new DateTime(2025, 11, 10), transaction.Date);
            Assert.Equal("Cargo TRF A COBAS Internacional FI", transaction.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_PositiveAmount_CreatesIncomeTransaction()
    {
        string csv = $"{Header}\n2025-10-01;2025-10-01;Abono nómina;Sin Categoría;240.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Transaction transaction = Assert.Single(txs);
            Assert.Equal(TransactionCategory.INCOME, transaction.Category);
            Assert.Equal(240.00m, transaction.Money.Amount);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TfiRecibida_BecomesTransfer()
    {
        string csv = $"{Header}\n2025-10-01;2025-10-01;TFI RECIBIDA;Sin Categoría;240.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Equal(TransactionCategory.TRANSFER, Assert.Single(txs).Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_InteresesDeposito_BecomesInterest()
    {
        string csv = $"{Header}\n2025-10-01;2025-10-01;INTERESES DEPOSITO;Sin Categoría;75.40;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Equal(TransactionCategory.INTEREST, Assert.Single(txs).Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_AperturaDeposito_BecomesTransfer()
    {
        string csv = $"{Header}\n2025-10-01;2025-10-01;APERTURA DEPOSITO A PLAZO;Sin Categoría;-10000.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Equal(TransactionCategory.TRANSFER, Assert.Single(txs).Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ClubTriatlo_BecomesExpense()
    {
        string csv = $"{Header}\n2025-10-01;2025-10-01;CLUB TRIATLO GRANOLLERS;Sin Categoría;-9.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Equal(TransactionCategory.EXPENSE, Assert.Single(txs).Category);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ParseAllAsync_TransferDescription_BecomesTransfer()
    {
        string csv = $"{Header}\n2025-09-04;2025-09-04;Cargo TRF A FRANCESC GIRBAU LLISTUELLA;Sin Categoría;-120.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

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
        string csv = $"{Header}\n2025-01-01;2025-01-01;Movimiento nulo;Sin Categoría;0.00;";
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, csv, Encoding.Latin1);
            SelfBankAccountImportService service = new();

            var (txs, _, _) = await service.ParseAllAsync(file);

            Assert.Empty(txs);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

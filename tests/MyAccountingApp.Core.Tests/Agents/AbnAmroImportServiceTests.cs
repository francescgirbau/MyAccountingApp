using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.AbnAmro;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

namespace MyAccountingApp.Core.Tests.Agents;

public class AbnAmroImportServiceTests
{
    private const string Header = "accountNumber,mutationcode,transactiondate,valuedate,startsaldo,endsaldo,amount,description\n";

    [Fact]
    public async Task ParseAllAsync_SepaOverboeking_BecomesTransfer()
    {
        string csv = Header + "889774927,OVERBOEKING,20220103,20220103,0,0,-1000,\"/TRTP/SEPA OVERBOEKING/IBAN/DE22101308001031845161/BIC/BIWBDE33/NAME/Francesc Girbau Llistuel/EREF/NOTPROVIDED\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.TRANSFER, tx.Category);
        Assert.Equal(1000m, tx.Money.Amount);
    }

    [Fact]
    public async Task ParseAllAsync_SepaOverboekingIncoming_BecomesTransfer()
    {
        string csv = Header + "889774927,OVERBOEKING,20220103,20220103,0,0,2500,\"/TRTP/SEPA OVERBOEKING/IBAN/NL22ABNA0123456789/BIC/ABNANL2A/NAME/Savings Account/EREF/OVBK\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.TRANSFER, tx.Category);
    }

    [Fact]
    public async Task ParseAllAsync_Betaalpas_BecomesExpense()
    {
        string csv = Header + "889774927,BEA,20220101,20220101,0,0,-4.7,\"BEA NR:41443001 01.01.22/13.42 BAR MARIOLA MIRAVET\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.EXPENSE, tx.Category);
    }

    [Fact]
    public async Task ParseAllAsync_SepaIncasso_BecomesExpense()
    {
        string csv = Header + "889774927,ABNO,20220105,20220105,0,0,-50,\"/TRTP/SEPA Incasso algemeen doorlopend/IBAN/NL83ABNA0123456789/NAME/Energy Provider/EREF/INC123\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.EXPENSE, tx.Category);
    }

    [Fact]
    public async Task ParseAllAsync_SpectralSalary_BecomesIncome()
    {
        string csv = Header + "889774927,OVERBOEKING,20220125,20220125,0,0,3500,\"/TRTP/SEPA OVERBOEKING/IBAN/NL00ABNA0000000000/BIC/ABNANL2A/NAME/Spectral/EREF/SALARY\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.INCOME, tx.Category);
        Assert.False(tx.NeedsReview);
    }

    [Fact]
    public async Task ParseAllAsync_UnknownCodeWithExpenseKeyword_BecomesExpenseEvenWithPositiveAmount()
    {
        // Una transferència cap a Berta Galende (despesa) amb un mutation code no reconegut
        // abans queia al signe (amount positiu -> INCOME). Ara la descripció mana sobre el signe.
        string csv = Header + "889774927,XYZ,20220110,20220110,0,0,1500,\"/TRTP/SEPA OVERBOEKING/IBAN/ES0000000000000000000000/NAME/BERTA GALENDE\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.EXPENSE, tx.Category);
        Assert.False(tx.NeedsReview);
    }

    [Fact]
    public async Task ParseAllAsync_UnknownCodeNoKeyword_PositiveAmount_IncomeWithNeedsReview()
    {
        string csv = Header + "889774927,XYZ,20220111,20220111,0,0,300,\"SOME UNKNOWN DESCRIPTION\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.INCOME, tx.Category);
        Assert.True(tx.NeedsReview);
    }

    [Fact]
    public async Task ParseAllAsync_UnknownCodeNoKeyword_NegativeAmount_ExpenseWithNeedsReview()
    {
        string csv = Header + "889774927,XYZ,20220112,20220112,0,0,-45,\"SOME UNKNOWN PAYMENT\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.EXPENSE, tx.Category);
        Assert.True(tx.NeedsReview);
    }

    [Fact]
    public async Task ParseAllAsync_OverboekingWithKnownKeyword_DoesNotNeedReview()
    {
        string csv = Header + "889774927,OVERBOEKING,20220113,20220113,0,0,2000,\"/TRTP/SEPA OVERBOEKING/IBAN/NL00ABNA0000000000/BIC/ABNANL2A/NAME/Revolut/EREF/TOPUP\"";
        string path = CreateFixtureFile(csv);
        AbnAmroImportService service = new AbnAmroImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction tx = Assert.Single(transactions);
        Assert.Equal(TransactionCategory.TRANSFER, tx.Category);
        Assert.False(tx.NeedsReview);
    }

    private static string CreateFixtureFile(string csv)
    {
        string path = Path.Combine(Path.GetTempPath(), $"abn_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, csv);
        return path;
    }
}
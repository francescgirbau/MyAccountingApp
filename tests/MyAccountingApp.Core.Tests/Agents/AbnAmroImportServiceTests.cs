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
    }

    private static string CreateFixtureFile(string csv)
    {
        string path = Path.Combine(Path.GetTempPath(), $"abn_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, csv);
        return path;
    }
}
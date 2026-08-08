using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Services;

public class RevolutImportServiceTests : IDisposable
{
    private readonly string _tempFile;
    private readonly RevolutImportService _service = new();

    public RevolutImportServiceTests()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesDeposit()
    {
        this.WriteLines(new[] { "DEPOSIT,a,2024-12-19,r,Top up,1000.00,0.00,EUR,COMPLETED,x" });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.DEPOSIT, transaction.Category);
        Assert.Equal(1000, transaction.Money.Amount);
        Assert.Equal("EUR", transaction.Money.Currency);
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesCardPaymentAndRefund()
    {
        this.WriteLines(new[]
        {
            "CARD PAYMENT,a,2024-12-19,r,Supermarket,25.50,0.00,EUR,COMPLETED,x",
            "CARD REFUND,a,2024-12-19,r,Store,10.00,0.00,EUR,COMPLETED,x",
        });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        List<Transaction> list = tx.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(TransactionCategory.EXPENSE, list[0].Category);
        Assert.Equal(TransactionCategory.INCOME, list[1].Category);
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesTransferBySign()
    {
        this.WriteLines(new[]
        {
            "TRANSFER,a,2024-12-19,r,Payment out,-50.00,0.00,EUR,COMPLETED,x",
            "TRANSFER,a,2024-12-19,r,FROM CUENTA FLEXIBLE,300.00,0.00,EUR,COMPLETED,x",
            "TRANSFER,a,2024-12-19,r,Refund,75.00,0.00,EUR,COMPLETED,x",
        });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        List<Transaction> list = tx.ToList();
        Assert.Equal(3, list.Count);
        Assert.Equal(TransactionCategory.EXPENSE, list[0].Category);
        Assert.Equal(TransactionCategory.INCOME, list[1].Category);
        Assert.Equal(TransactionCategory.INCOME, list[2].Category);
    }

    [Fact]
    public async Task ParseAllAsync_AddsFeeTransaction()
    {
        this.WriteLines(new[] { "CARD PAYMENT,a,2024-12-19,r,Merchant,20.00,2.00,EUR,COMPLETED,x" });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        List<Transaction> list = tx.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(TransactionCategory.EXPENSE, list[1].Category);
        Assert.Equal("Merchant (fee)", list[1].Description);
        Assert.Equal(2, list[1].Money.Amount);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("REVERTED")]
    public async Task ParseAllAsync_SkipsNonCompleted(string state)
    {
        this.WriteLines(new[] { $"DEPOSIT,a,2024-12-19,r,Top up,1000.00,0.00,EUR,{state},x" });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        Assert.Empty(tx);
    }

    [Fact]
    public async Task ParseAllAsync_SkipsBalanceMigrationAndUnknownTypes()
    {
        this.WriteLines(new[]
        {
            "TRANSFER,a,2024-12-19,r,BALANCE MIGRATION,500.00,0.00,EUR,COMPLETED,x",
            "EXCHANGE,a,2024-12-19,r,Currency exchange,100.00,0.00,EUR,COMPLETED,x",
            "ATM,a,2024-12-19,r,Cash,40.00,0.00,EUR,COMPLETED,x",
        });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.EXPENSE, transaction.Category);
    }

    [Fact]
    public async Task ParseAllAsync_SkipsZeroAmountAndInvalidLines()
    {
        this.WriteLines(new[]
        {
            "DEPOSIT,a,2024-12-19,r,Zero,0.00,0.00,EUR,COMPLETED,x",
            "DEPOSIT,a,2024-12-19,r,Bad amount,abc,0.00,EUR,COMPLETED,x",
            "DEPOSIT,a,2024-12-19,r,Short line,100.00",
        });

        (IEnumerable<Transaction> tx, _, _) = await this._service.ParseAllAsync(this._tempFile);

        Assert.Empty(tx);
    }

    [Fact]
    public async Task ParseAllAsync_Throws_WhenPathNullOrWhiteSpace()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => this._service.ParseAllAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => this._service.ParseAllAsync(null!));
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty()
    {
        IEnumerable<AssetTransaction> result = await this._service.ParseCorporateActionsAsync("whatever.csv");
        Assert.Empty(result);
    }

    private void WriteLines(IEnumerable<string> lines)
    {
        string header = "Type,Account,Date,Reference,Description,Amount,Fee,Currency,State,Extra";
        File.WriteAllLines(this._tempFile, new[] { header }.Concat(lines));
    }
}

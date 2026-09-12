using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonLoanMovementRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonLoanMovementRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileDoesNotExist()
    {
        JsonLoanMovementRepository repo = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileIsCorrupt()
    {
        File.WriteAllText(this._tempFile, "not valid json {{{");
        JsonLoanMovementRepository repo = new(this._tempFile);

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Add_ThenGetAll_RoundTripsPreservingAllFields()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        Guid id = Guid.NewGuid();
        Guid loanId = Guid.NewGuid();
        Transaction transaction = new(id, new DateTime(2025, 3, 1), "Loan from Berta", new Money(5000, "EUR"), TransactionCategory.DEPOSIT);
        repo.Add(new LoanMovement(Guid.NewGuid(), loanId, transaction, LoanMovementType.Disbursement));

        LoanMovement loaded = Assert.Single(repo.GetAll());
        Assert.Equal(loanId, loaded.LoanId);
        Assert.Equal(LoanMovementType.Disbursement, loaded.Type);
        Assert.Equal(5000m, loaded.Transaction.Money.Amount);
        Assert.Equal("Loan from Berta", loaded.Transaction.Description);
    }

    [Fact]
    public void GetByLoan_ReturnsOnlyMovementsForLoan()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        repo.Add(CreateMovement(first, new DateTime(2025, 3, 1), 1000m, LoanMovementType.Disbursement));
        repo.Add(CreateMovement(first, new DateTime(2025, 4, 1), 200m, LoanMovementType.Repayment));
        repo.Add(CreateMovement(second, new DateTime(2025, 5, 1), 500m, LoanMovementType.Disbursement));

        IEnumerable<LoanMovement> result = repo.GetByLoan(first);

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public void Update_ReplacesExistingMovement()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        Guid loanId = Guid.NewGuid();
        LoanMovement original = CreateMovement(loanId, new DateTime(2025, 3, 1), 1000m, LoanMovementType.Disbursement);
        repo.Add(original);

        LoanMovement updated = new(original.Id, loanId, new Transaction(Guid.NewGuid(), new DateTime(2025, 3, 1), "Loan from Berta", new Money(1500, "EUR"), TransactionCategory.DEPOSIT), LoanMovementType.Disbursement);
        repo.Update(updated);

        LoanMovement loaded = Assert.Single(repo.GetAll());
        Assert.Equal(1500m, loaded.Transaction.Money.Amount);
    }

    [Fact]
    public void Delete_RemovesAndReturnsTrue()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        LoanMovement movement = CreateMovement(Guid.NewGuid(), new DateTime(2025, 3, 1), 1000m, LoanMovementType.Disbursement);
        repo.Add(movement);

        bool deleted = repo.Delete(movement.Id);

        Assert.True(deleted);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void DeleteByYear_RemovesOnlyMatchingYear()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        repo.Add(CreateMovement(Guid.NewGuid(), new DateTime(2024, 5, 1), 1000m, LoanMovementType.Disbursement));
        repo.Add(CreateMovement(Guid.NewGuid(), new DateTime(2025, 5, 1), 500m, LoanMovementType.Repayment));

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(1, removed);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void Initialize_OverwritesExistingData()
    {
        JsonLoanMovementRepository repo = new(this._tempFile);
        repo.Add(CreateMovement(Guid.NewGuid(), new DateTime(2025, 3, 1), 1000m, LoanMovementType.Disbursement));

        repo.Initialize(new[] { CreateMovement(Guid.NewGuid(), new DateTime(2026, 1, 1), 200m, LoanMovementType.Repayment) });

        Assert.Single(repo.GetAll());
        Assert.Equal(200m, repo.GetAll().Single().Transaction.Money.Amount);
    }

    private static LoanMovement CreateMovement(Guid loanId, DateTime date, decimal amount, LoanMovementType type)
    {
        Transaction transaction = new(date, "Loan movement", new Money(amount, "EUR"), TransactionCategory.DEPOSIT);
        return new LoanMovement(Guid.NewGuid(), loanId, transaction, type);
    }
}
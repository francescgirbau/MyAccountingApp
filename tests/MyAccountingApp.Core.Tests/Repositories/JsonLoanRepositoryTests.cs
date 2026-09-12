using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonLoanRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonLoanRepositoryTests()
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
        JsonLoanRepository repo = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileIsCorrupt()
    {
        File.WriteAllText(this._tempFile, "not valid json {{{");
        JsonLoanRepository repo = new(this._tempFile);

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Add_ThenGetAll_RoundTripsPreservingAllFields()
    {
        JsonLoanRepository repo = new(this._tempFile);
        Guid id = Guid.NewGuid();
        Loan loan = new(id, "Berta", LoanDirection.Borrowed, new Money(5000, "EUR"), new DateTime(2025, 3, 1), "Personal loan");
        loan.Close();
        repo.Add(loan);

        Loan loaded = Assert.Single(repo.GetAll());
        Assert.Equal(id, loaded.Id);
        Assert.Equal("Berta", loaded.Counterparty);
        Assert.Equal(LoanDirection.Borrowed, loaded.Direction);
        Assert.Equal(5000m, loaded.Principal.Amount);
        Assert.Equal("EUR", loaded.Principal.Currency);
        Assert.Equal(new DateTime(2025, 3, 1), loaded.StartDate);
        Assert.Equal("Personal loan", loaded.Notes);
        Assert.True(loaded.IsClosed);
    }

    [Fact]
    public void GetById_ReturnsLoan_WhenFound()
    {
        JsonLoanRepository repo = new(this._tempFile);
        Guid id = Guid.NewGuid();
        repo.Add(new Loan(id, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        Loan? loaded = repo.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded!.Id);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        JsonLoanRepository repo = new(this._tempFile);

        Assert.Null(repo.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void Update_ReplacesExistingLoan()
    {
        JsonLoanRepository repo = new(this._tempFile);
        Guid id = Guid.NewGuid();
        Loan original = new(id, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1));
        repo.Add(original);

        Loan updated = new(id, "Maria", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1));
        repo.Update(updated);

        Loan loaded = Assert.Single(repo.GetAll());
        Assert.Equal("Maria", loaded.Counterparty);
    }

    [Fact]
    public void Update_DoesNothing_WhenIdNotFound()
    {
        JsonLoanRepository repo = new(this._tempFile);
        repo.Add(new Loan(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        repo.Update(new Loan(Guid.NewGuid(), "Maria", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void Delete_RemovesAndReturnsTrue()
    {
        JsonLoanRepository repo = new(this._tempFile);
        Guid id = Guid.NewGuid();
        repo.Add(new Loan(id, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        bool deleted = repo.Delete(id);

        Assert.True(deleted);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdNotFound()
    {
        JsonLoanRepository repo = new(this._tempFile);
        repo.Add(new Loan(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        bool deleted = repo.Delete(Guid.NewGuid());

        Assert.False(deleted);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void Initialize_OverwritesExistingData()
    {
        JsonLoanRepository repo = new(this._tempFile);
        repo.Add(new Loan(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));

        repo.Initialize(new[] { new Loan(Guid.NewGuid(), "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2024, 1, 1)) });

        Loan loaded = Assert.Single(repo.GetAll());
        Assert.Equal("Pere", loaded.Counterparty);
        Assert.Equal(LoanDirection.Lent, loaded.Direction);
    }
}
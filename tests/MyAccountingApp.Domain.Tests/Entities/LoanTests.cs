using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Tests.Entities;

public class LoanTests
{
    [Fact]
    public void Ctor_ShouldSetProperties_WhenValid()
    {
        DateTime start = new(2025, 3, 1);
        Loan loan = new(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(5000, "EUR"), start, "Personal loan");

        Assert.Equal("Berta", loan.Counterparty);
        Assert.Equal(LoanDirection.Borrowed, loan.Direction);
        Assert.Equal(5000m, loan.Principal.Amount);
        Assert.Equal("EUR", loan.Principal.Currency);
        Assert.Equal(start, loan.StartDate);
        Assert.Equal("Personal loan", loan.Notes);
        Assert.False(loan.IsClosed);
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenCounterpartyIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new Loan(Guid.NewGuid(), " ", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenPrincipalIsNotPositive()
    {
        Assert.Throws<ArgumentException>(() => new Loan(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(0, "EUR"), new DateTime(2025, 3, 1)));
        Assert.Throws<ArgumentException>(() => new Loan(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(-50, "EUR"), new DateTime(2025, 3, 1)));
    }

    [Fact]
    public void Rename_ShouldChangeCounterparty_WhenValid()
    {
        Loan loan = new(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1));

        loan.Rename("Maria");

        Assert.Equal("Maria", loan.Counterparty);
    }

    [Fact]
    public void UpdateNotes_ShouldReplaceNotes()
    {
        Loan loan = new(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1));

        loan.UpdateNotes(null);

        Assert.Null(loan.Notes);
    }

    [Fact]
    public void CloseAndReopen_ShouldToggleIsClosed()
    {
        Loan loan = new(Guid.NewGuid(), "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1));

        loan.Close();
        Assert.True(loan.IsClosed);

        loan.Reopen();
        Assert.False(loan.IsClosed);
    }
}
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Tests.Entities;

public class LoanMovementTests
{
    [Fact]
    public void Ctor_ShouldSetProperties_WhenValid()
    {
        Guid loanId = Guid.NewGuid();
        Transaction transaction = new(new DateTime(2025, 3, 1), "Loan from Berta", new Money(5000, "EUR"), TransactionCategory.DEPOSIT);

        LoanMovement movement = new(Guid.NewGuid(), loanId, transaction, LoanMovementType.Disbursement);

        Assert.Equal(loanId, movement.LoanId);
        Assert.Equal(LoanMovementType.Disbursement, movement.Type);
        Assert.Same(transaction, movement.Transaction);
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenLoanIdIsEmpty()
    {
        Transaction transaction = new(new DateTime(2025, 3, 1), "Loan from Berta", new Money(5000, "EUR"), TransactionCategory.DEPOSIT);

        Assert.Throws<ArgumentException>(() => new LoanMovement(Guid.NewGuid(), Guid.Empty, transaction, LoanMovementType.Disbursement));
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenTransactionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LoanMovement(Guid.NewGuid(), Guid.NewGuid(), null!, LoanMovementType.Repayment));
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenAmountIsNotPositive()
    {
        Transaction transaction = new(Guid.Empty, new DateTime(2025, 3, 1), "Loan from Berta", new Money(-5, "EUR"), TransactionCategory.DEPOSIT);

        Assert.Throws<ArgumentException>(() => new LoanMovement(Guid.NewGuid(), Guid.NewGuid(), transaction, LoanMovementType.Disbursement));
    }
}
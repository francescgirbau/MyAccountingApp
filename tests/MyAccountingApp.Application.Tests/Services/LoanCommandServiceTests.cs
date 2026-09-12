using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class LoanCommandServiceTests
{
    [Fact]
    public void CreateLoan_AddsLoanAndDisbursement_WithDepositCategory_WhenBorrowed()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto created = service.CreateLoan(new CreateLoanRequest(
            new DateTime(2025, 3, 1),
            "Berta",
            1000m,
            "EUR",
            "Borrowed",
            "Personal loan"));

        Loan loan = Assert.Single(loanRepo.GetAll());
        Assert.Equal("Berta", loan.Counterparty);
        Assert.Equal(LoanDirection.Borrowed, loan.Direction);
        Assert.Equal(1000m, loan.Principal.Amount);
        Assert.Equal("Personal loan", loan.Notes);

        LoanMovement disbursement = Assert.Single(movementRepo.GetAll());
        Assert.Equal(LoanMovementType.Disbursement, disbursement.Type);
        Assert.Equal(loan.Id, disbursement.LoanId);
        Assert.Equal(1000m, disbursement.Transaction.Money.Amount);
        Assert.Equal(TransactionCategory.DEPOSIT, disbursement.Transaction.Category);

        Assert.Equal(loan.Id, created.LoanId);
        Assert.Equal(1000m, created.Principal);
        Assert.Equal(0m, created.Repaid);
        Assert.Equal(1000m, created.Outstanding);
    }

    [Fact]
    public void CreateLoan_AddsDisbursement_WithTransferCategory_WhenLent()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        service.CreateLoan(new CreateLoanRequest(
            new DateTime(2025, 3, 1),
            "Pere",
            500m,
            "EUR",
            "Lent"));

        LoanMovement disbursement = Assert.Single(movementRepo.GetAll());
        Assert.Equal(TransactionCategory.TRANSFER, disbursement.Transaction.Category);
        Assert.Equal("Loan to Pere", disbursement.Transaction.Description);
    }

    [Fact]
    public void CreateLoan_Throws_WhenDirectionInvalid()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.Throws<ArgumentException>(() => service.CreateLoan(new CreateLoanRequest(
            new DateTime(2025, 3, 1),
            "Berta",
            1000m,
            "EUR",
            "Sideways")));
    }

    [Fact]
    public void CreateLoan_Throws_WhenAmountNotPositive()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.Throws<ArgumentException>(() => service.CreateLoan(new CreateLoanRequest(
            new DateTime(2025, 3, 1),
            "Berta",
            0m,
            "EUR",
            "Borrowed")));
    }

    [Fact]
    public void AddRepayment_CreatesRepayment_WithTransferCategory_WhenBorrowed()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto updated = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 250m));

        LoanMovement repayment = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Repayment);
        Assert.Equal(250m, repayment.Transaction.Money.Amount);
        Assert.Equal(TransactionCategory.TRANSFER, repayment.Transaction.Category);
        Assert.Equal("Repayment to Berta", repayment.Transaction.Description);
        Assert.Equal(250m, updated.Repaid);
        Assert.Equal(750m, updated.Outstanding);
    }

    [Fact]
    public void AddRepayment_CreatesRepayment_WithDepositCategory_WhenLent()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 5, 1), 100m));

        LoanMovement repayment = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Repayment);
        Assert.Equal(TransactionCategory.DEPOSIT, repayment.Transaction.Category);
        Assert.Equal("Repayment from Pere", repayment.Transaction.Description);
    }

    [Fact]
    public void AddRepayment_Throws_WhenLoanMissing()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.Throws<InvalidOperationException>(() => service.AddRepayment(Guid.NewGuid(), new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 100m)));
    }

    [Fact]
    public void AddRepayment_Throws_WhenAmountNotPositive()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        Assert.Throws<ArgumentException>(() => service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 0m)));
    }

    [Fact]
    public void CloseLoan_MarksLoanClosed()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        bool closed = service.CloseLoan(loanId);

        Assert.True(closed);
        Loan? updated = loanRepo.GetById(loanId);
        Assert.True(updated is not null);
        Assert.True(updated.IsClosed);
    }

    [Fact]
    public void CloseLoan_ReturnsFalse_WhenMissing()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.False(service.CloseLoan(Guid.NewGuid()));
    }

    [Fact]
    public void DeleteLoan_CascadesToMovements()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        movementRepo.Add(CreateMovement(loanId, 200, new DateTime(2025, 4, 1), LoanMovementType.Repayment));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        bool deleted = service.DeleteLoan(loanId);

        Assert.True(deleted);
        Assert.Empty(loanRepo.GetAll());
        Assert.Empty(movementRepo.GetByLoan(loanId));
    }

    [Fact]
    public void DeleteLoan_ReturnsFalse_WhenMissing()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.False(service.DeleteLoan(Guid.NewGuid()));
    }

    private static LoanMovement CreateMovement(Guid loanId, decimal amount, DateTime date, LoanMovementType type)
    {
        Transaction transaction = new(date, "Loan movement", new Money(amount, "EUR"), TransactionCategory.DEPOSIT);
        return new LoanMovement(Guid.NewGuid(), loanId, transaction, type);
    }
}
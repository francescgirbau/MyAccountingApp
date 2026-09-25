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
    public void CreateLoan_AddsLoanAndDisbursement_WithLoanInCategory_WhenBorrowed()
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
        Assert.Equal(TransactionCategory.LOAN_IN, disbursement.Transaction.Category);

        Assert.Equal(loan.Id, created.LoanId);
        Assert.Equal(1000m, created.Principal);
        Assert.Equal(0m, created.Repaid);
        Assert.Equal(1000m, created.Outstanding);
    }

    [Fact]
    public void CreateLoan_AddsDisbursement_WithLoanOutCategory_WhenLent()
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
        Assert.Equal(TransactionCategory.LOAN_OUT, disbursement.Transaction.Category);
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
    public void AddRepayment_CreatesRepayment_WithLoanOutCategory_WhenBorrowed()
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
        Assert.Equal(TransactionCategory.LOAN_OUT, repayment.Transaction.Category);
        Assert.Equal("Repayment to Berta", repayment.Transaction.Description);
        Assert.Equal(250m, updated.Repaid);
        Assert.Equal(750m, updated.Outstanding);
    }

    [Fact]
    public void AddRepayment_CreatesRepayment_WithLoanInCategory_WhenLent()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 5, 1), 100m));

        LoanMovement repayment = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Repayment);
        Assert.Equal(TransactionCategory.LOAN_IN, repayment.Transaction.Category);
        Assert.Equal("Repayment from Pere", repayment.Transaction.Description);
    }

    [Fact]
    public void AddRepayment_WithInterest_CreatesCapitalAndInterestMovements_WhenBorrowed()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto updated = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 200m, 50m));

        LoanMovement repayment = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Repayment);
        Assert.Equal(200m, repayment.Transaction.Money.Amount);
        Assert.Equal(TransactionCategory.LOAN_OUT, repayment.Transaction.Category);
        Assert.Equal("Repayment to Berta", repayment.Transaction.Description);

        LoanMovement interest = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Interest);
        Assert.Equal(50m, interest.Transaction.Money.Amount);
        Assert.Equal(TransactionCategory.LOAN_OUT, interest.Transaction.Category);
        Assert.Equal("Interest paid to Berta", interest.Transaction.Description);

        Assert.Equal(200m, updated.Repaid);
        Assert.Equal(50m, updated.InterestPaid);
        Assert.Equal(800m, updated.Outstanding);
    }

    [Fact]
    public void AddRepayment_WithInterest_WhenLent_InterestIsCashIn()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto updated = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 5, 1), 100m, 25m));

        LoanMovement interest = movementRepo.GetByLoan(loanId).Single(m => m.Type == LoanMovementType.Interest);
        Assert.Equal(TransactionCategory.LOAN_IN, interest.Transaction.Category);
        Assert.Equal("Interest received from Pere", interest.Transaction.Description);
        Assert.Equal(100m, updated.Repaid);
        Assert.Equal(25m, updated.InterestPaid);
        Assert.Equal(400m, updated.Outstanding);
    }

    [Fact]
    public void AddRepayment_InterestOnly_CreatesOnlyInterestMovement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto updated = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 0m, 50m));

        LoanMovement interest = Assert.Single(movementRepo.GetByLoan(loanId), m => m.Type != LoanMovementType.Disbursement);
        Assert.Equal(LoanMovementType.Interest, interest.Type);
        Assert.Equal(0m, updated.Repaid);
        Assert.Equal(50m, updated.InterestPaid);
        Assert.Equal(1000m, updated.Outstanding);
    }

    [Fact]
    public void AddRepayment_Throws_WhenCapitalAndInterestBothZero()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        Assert.Throws<ArgumentException>(() => service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 0m, 0m)));
    }

    [Fact]
    public void AddRepayment_Throws_WhenInterestNegative()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        Assert.Throws<ArgumentException>(() => service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 100m, -5m)));
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

    [Fact]
    public void UpdateMovement_ChangesAmountDateAndType_RecategorizesMovement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto created = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 250m));
        Guid repaymentId = created.Movements.Single(m => m.Type == "Repayment").Id;

        LoanSummaryDto updated = service.UpdateMovement(loanId, repaymentId, new UpdateLoanMovementRequest(new DateTime(2025, 5, 1), 300m, "Interest"));

        LoanMovement movement = movementRepo.GetByLoan(loanId).Single(m => m.Id == repaymentId);
        Assert.Equal(300m, movement.Transaction.Money.Amount);
        Assert.Equal(new DateTime(2025, 5, 1), movement.Transaction.Date);
        Assert.Equal(LoanMovementType.Interest, movement.Type);
        Assert.Equal(TransactionCategory.LOAN_OUT, movement.Transaction.Category);
        Assert.Equal("Interest paid to Berta", movement.Transaction.Description);

        Assert.Equal(0m, updated.Repaid);
        Assert.Equal(300m, updated.InterestPaid);
        Assert.Equal(1000m, updated.Outstanding);
    }

    [Fact]
    public void UpdateMovement_ToCapital_RecategorizesToRepayment()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto created = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 0m, 25m));
        Guid interestId = created.Movements.Single(m => m.Type == "Interest").Id;

        LoanSummaryDto updated = service.UpdateMovement(loanId, interestId, new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Repayment"));

        LoanMovement movement = movementRepo.GetByLoan(loanId).Single(m => m.Id == interestId);
        Assert.Equal(LoanMovementType.Repayment, movement.Type);
        Assert.Equal(TransactionCategory.LOAN_IN, movement.Transaction.Category);
        Assert.Equal("Repayment from Pere", movement.Transaction.Description);
        Assert.Equal(100m, updated.Repaid);
        Assert.Equal(0m, updated.InterestPaid);
        Assert.Equal(400m, updated.Outstanding);
    }

    [Fact]
    public void UpdateMovement_Throws_WhenLoanMissing()
    {
        LoanCommandService service = new(new FakeLoanRepository(), new FakeLoanMovementRepository(), new LoanQuery(new FakeLoanRepository(), new FakeLoanMovementRepository()));

        Assert.Throws<InvalidOperationException>(() => service.UpdateMovement(Guid.NewGuid(), Guid.NewGuid(), new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Repayment")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenMovementMissing()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        Assert.Throws<InvalidOperationException>(() => service.UpdateMovement(loanId, Guid.NewGuid(), new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Repayment")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenMovementBelongsToAnotherLoan()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        Guid otherLoanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        loanRepo.Add(new Loan(otherLoanId, "Pere", LoanDirection.Lent, new Money(500, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(otherLoanId, 100, new DateTime(2025, 4, 1), LoanMovementType.Repayment));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Guid foreignMovementId = movementRepo.GetByLoan(otherLoanId).Single().Id;

        Assert.Throws<InvalidOperationException>(() => service.UpdateMovement(loanId, foreignMovementId, new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Repayment")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenEditingDisbursement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanMovement disbursement = CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement);
        movementRepo.Add(disbursement);
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Assert.Throws<ArgumentException>(() => service.UpdateMovement(loanId, disbursement.Id, new UpdateLoanMovementRequest(new DateTime(2025, 3, 1), 900m, "Disbursement")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenAmountNotPositive()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanMovement repayment = CreateMovement(loanId, 100, new DateTime(2025, 4, 1), LoanMovementType.Repayment);
        movementRepo.Add(repayment);
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Assert.Throws<ArgumentException>(() => service.UpdateMovement(loanId, repayment.Id, new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 0m, "Repayment")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenTypeInvalid()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanMovement repayment = CreateMovement(loanId, 100, new DateTime(2025, 4, 1), LoanMovementType.Repayment);
        movementRepo.Add(repayment);
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Assert.Throws<ArgumentException>(() => service.UpdateMovement(loanId, repayment.Id, new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Refund")));
    }

    [Fact]
    public void UpdateMovement_Throws_WhenTypeIsDisbursement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanMovement repayment = CreateMovement(loanId, 100, new DateTime(2025, 4, 1), LoanMovementType.Repayment);
        movementRepo.Add(repayment);
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Assert.Throws<ArgumentException>(() => service.UpdateMovement(loanId, repayment.Id, new UpdateLoanMovementRequest(new DateTime(2025, 4, 1), 100m, "Disbursement")));
    }

    [Fact]
    public void DeleteMovement_RemovesOnlyThatMovement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        LoanSummaryDto created = service.AddRepayment(loanId, new AddLoanRepaymentRequest(new DateTime(2025, 4, 1), 250m));
        Guid repaymentId = created.Movements.Single(m => m.Type == "Repayment").Id;

        bool deleted = service.DeleteMovement(loanId, repaymentId);

        Assert.True(deleted);
        Assert.DoesNotContain(movementRepo.GetByLoan(loanId), m => m.Id == repaymentId);
        Assert.Single(movementRepo.GetByLoan(loanId));
        LoanQuery query = new(loanRepo, movementRepo);
        LoanSummaryDto? updated = query.GetById(loanId);
        Assert.NotNull(updated);
        Assert.Equal(0m, updated.Repaid);
        Assert.Equal(1000m, updated.Outstanding);
    }

    [Fact]
    public void DeleteMovement_ReturnsFalse_WhenMissing()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanCommandService service = new(loanRepo, new FakeLoanMovementRepository(), new LoanQuery(loanRepo, new FakeLoanMovementRepository()));

        Assert.False(service.DeleteMovement(loanId, Guid.NewGuid()));
    }

    [Fact]
    public void DeleteMovement_Throws_WhenDeletingDisbursement()
    {
        FakeLoanRepository loanRepo = new();
        FakeLoanMovementRepository movementRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(new Loan(loanId, "Berta", LoanDirection.Borrowed, new Money(1000, "EUR"), new DateTime(2025, 3, 1)));
        LoanMovement disbursement = CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement);
        movementRepo.Add(disbursement);
        LoanCommandService service = new(loanRepo, movementRepo, new LoanQuery(loanRepo, movementRepo));

        Assert.Throws<ArgumentException>(() => service.DeleteMovement(loanId, disbursement.Id));
    }

    private static LoanMovement CreateMovement(Guid loanId, decimal amount, DateTime date, LoanMovementType type)
    {
        Transaction transaction = new(date, "Loan movement", new Money(amount, "EUR"), TransactionCategory.LOAN_IN);
        return new LoanMovement(Guid.NewGuid(), loanId, transaction, type);
    }
}
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class LoanQueryTests
{
    [Fact]
    public void GetAll_ComputesOutstanding_WithPartialRepayments()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(CreateLoan(loanId, principal: 1000));
        FakeLoanMovementRepository movementRepo = new();
        movementRepo.Add(CreateMovement(loanId, 200, new DateTime(2025, 4, 1), LoanMovementType.Repayment));
        movementRepo.Add(CreateMovement(loanId, 150, new DateTime(2025, 5, 1), LoanMovementType.Repayment));
        movementRepo.Add(CreateMovement(loanId, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        LoanQuery query = new(loanRepo, movementRepo);

        LoanSummaryDto summary = Assert.Single(query.GetAll());

        Assert.Equal("Berta", summary.Counterparty);
        Assert.Equal("Borrowed", summary.Direction);
        Assert.Equal(1000m, summary.Principal);
        Assert.Equal(350m, summary.Repaid);
        Assert.Equal(650m, summary.Outstanding);
        Assert.False(summary.IsOverpaid);
        Assert.False(summary.IsClosed);
    }

    [Fact]
    public void GetAll_ListsFullyRepaidLoan_UntilClosed()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(CreateLoan(loanId, principal: 500));
        FakeLoanMovementRepository movementRepo = new();
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        movementRepo.Add(CreateMovement(loanId, 500, new DateTime(2025, 6, 1), LoanMovementType.Repayment));
        LoanQuery query = new(loanRepo, movementRepo);

        LoanSummaryDto summary = Assert.Single(query.GetAll());

        Assert.Equal(0m, summary.Outstanding);
        Assert.False(summary.IsOverpaid);
    }

    [Fact]
    public void GetAll_FlagsOverpaidLoan_WithNegativeOutstanding()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(CreateLoan(loanId, principal: 300));
        FakeLoanMovementRepository movementRepo = new();
        movementRepo.Add(CreateMovement(loanId, 300, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        movementRepo.Add(CreateMovement(loanId, 300, new DateTime(2025, 4, 1), LoanMovementType.Repayment));
        movementRepo.Add(CreateMovement(loanId, 50, new DateTime(2025, 5, 1), LoanMovementType.Repayment));
        LoanQuery query = new(loanRepo, movementRepo);

        LoanSummaryDto summary = Assert.Single(query.GetAll());

        Assert.Equal(350m, summary.Repaid);
        Assert.Equal(-50m, summary.Outstanding);
        Assert.True(summary.IsOverpaid);
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoLoans()
    {
        LoanQuery query = new(new FakeLoanRepository(), new FakeLoanMovementRepository());

        Assert.Empty(query.GetAll());
    }

    [Fact]
    public void GetAll_SetsLastMovementDate_AndIsolationBetweenLoans()
    {
        FakeLoanRepository loanRepo = new();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        loanRepo.Add(CreateLoan(first, principal: 1000));
        loanRepo.Add(CreateLoan(second, principal: 500, direction: LoanDirection.Lent));
        FakeLoanMovementRepository movementRepo = new();
        movementRepo.Add(CreateMovement(first, 1000, new DateTime(2025, 3, 1), LoanMovementType.Disbursement));
        movementRepo.Add(CreateMovement(first, 100, new DateTime(2025, 7, 15), LoanMovementType.Repayment));
        movementRepo.Add(CreateMovement(second, 500, new DateTime(2024, 9, 1), LoanMovementType.Disbursement));
        LoanQuery query = new(loanRepo, movementRepo);

        List<LoanSummaryDto> results = query.GetAll();

        LoanSummaryDto borrowed = results.Single(r => r.LoanId == first);
        LoanSummaryDto lent = results.Single(r => r.LoanId == second);
        Assert.Equal(new DateTime(2025, 7, 15), borrowed.LastMovementDate);
        Assert.Equal(900m, borrowed.Outstanding);
        Assert.Equal("Lent", lent.Direction);
        Assert.Equal(500m, lent.Outstanding);
        Assert.Equal(new DateTime(2024, 9, 1), lent.LastMovementDate);
    }

    [Fact]
    public void GetById_ReturnsSummary_WhenFound()
    {
        FakeLoanRepository loanRepo = new();
        Guid loanId = Guid.NewGuid();
        loanRepo.Add(CreateLoan(loanId, principal: 1000));
        LoanQuery query = new(loanRepo, new FakeLoanMovementRepository());

        LoanSummaryDto? summary = query.GetById(loanId);

        Assert.NotNull(summary);
        Assert.Equal(1000m, summary!.Principal);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        LoanQuery query = new(new FakeLoanRepository(), new FakeLoanMovementRepository());

        Assert.Null(query.GetById(Guid.NewGuid()));
    }

    private static Loan CreateLoan(Guid id, decimal principal, LoanDirection direction = LoanDirection.Borrowed) =>
        new(id, "Berta", direction, new Money(principal, "EUR"), new DateTime(2025, 3, 1));

    private static LoanMovement CreateMovement(Guid loanId, decimal amount, DateTime date, LoanMovementType type)
    {
        Transaction transaction = new(date, "Loan movement", new Money(amount, "EUR"), TransactionCategory.DEPOSIT);
        return new LoanMovement(Guid.NewGuid(), loanId, transaction, type);
    }
}
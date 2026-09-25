using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public sealed class LoanQuery : ILoanQuery
{
    private readonly ILoanRepository _loanRepo;
    private readonly ILoanMovementRepository _movementRepo;

    public LoanQuery(ILoanRepository loanRepo, ILoanMovementRepository movementRepo)
    {
        this._loanRepo = loanRepo;
        this._movementRepo = movementRepo;
    }

    public List<LoanSummaryDto> GetAll()
    {
        List<Loan> loans = this._loanRepo.GetAll().ToList();
        List<LoanMovement> movements = this._movementRepo.GetAll().ToList();
        return loans.Select(loan => this.ToSummary(loan, movements)).ToList();
    }

    public LoanSummaryDto? GetById(Guid loanId)
    {
        Loan? loan = this._loanRepo.GetById(loanId);
        return loan is null ? null : this.ToSummary(loan, this._movementRepo.GetByLoan(loanId));
    }

    private LoanSummaryDto ToSummary(Loan loan, IEnumerable<LoanMovement> movements)
    {
        List<LoanMovement> loanMovements = movements.Where(m => m.LoanId == loan.Id).ToList();
        decimal repaid = loanMovements
            .Where(m => m.Type == LoanMovementType.Repayment)
            .Sum(m => m.Transaction.Money.Amount);
        decimal interestPaid = loanMovements
            .Where(m => m.Type == LoanMovementType.Interest)
            .Sum(m => m.Transaction.Money.Amount);
        decimal outstanding = loan.Principal.Amount - repaid;
        DateTime? lastMovementDate = loanMovements.Count > 0
            ? loanMovements.Max(m => m.Transaction.Date)
            : null;

        return new LoanSummaryDto(
            loan.Id,
            loan.Counterparty,
            loan.Direction.ToString(),
            Math.Round(loan.Principal.Amount, 2),
            loan.Principal.Currency,
            Math.Round(repaid, 2),
            Math.Round(interestPaid, 2),
            Math.Round(outstanding, 2),
            outstanding < 0,
            loan.IsClosed,
            loan.StartDate,
            lastMovementDate,
            loanMovements
                .OrderBy(m => m.Transaction.Date)
                .Select(m => new LoanMovementDto(
                    m.Id,
                    m.Transaction.Date,
                    m.Type.ToString(),
                    m.Transaction.Description,
                    Math.Round(m.Transaction.Money.Amount, 2),
                    m.Transaction.Money.Currency))
                .ToList());
    }
}
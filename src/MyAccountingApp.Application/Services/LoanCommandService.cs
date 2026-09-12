using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

public sealed class LoanCommandService : ILoanCommandService
{
    private readonly ILoanRepository _loanRepo;
    private readonly ILoanMovementRepository _movementRepo;
    private readonly ILoanQuery _loanQuery;

    public LoanCommandService(ILoanRepository loanRepo, ILoanMovementRepository movementRepo, ILoanQuery loanQuery)
    {
        this._loanRepo = loanRepo;
        this._movementRepo = movementRepo;
        this._loanQuery = loanQuery;
    }

    public LoanSummaryDto CreateLoan(CreateLoanRequest request)
    {
        if (!Enum.TryParse<LoanDirection>(request.Direction, ignoreCase: true, out LoanDirection direction))
        {
            throw new ArgumentException($"Invalid direction: {request.Direction}", nameof(request.Direction));
        }

        Money principal = new(request.Amount, request.Currency);
        Loan loan = new(Guid.NewGuid(), request.Counterparty, direction, principal, request.StartDate, request.Notes);
        this._loanRepo.Add(loan);

        LoanMovement disbursement = this.CreateMovement(loan, principal.Amount, request.StartDate, LoanMovementType.Disbursement);
        this._movementRepo.Add(disbursement);

        return this._loanQuery.GetById(loan.Id)!;
    }

    public LoanSummaryDto AddRepayment(Guid loanId, AddLoanRepaymentRequest request)
    {
        Loan? loan = this._loanRepo.GetById(loanId);
        if (loan is null)
        {
            throw new InvalidOperationException("Loan not found.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Repayment amount must be greater than zero.", nameof(request.Amount));
        }

        LoanMovement repayment = this.CreateMovement(loan, request.Amount, request.Date, LoanMovementType.Repayment);
        this._movementRepo.Add(repayment);

        return this._loanQuery.GetById(loanId)!;
    }

    public bool CloseLoan(Guid loanId)
    {
        Loan? loan = this._loanRepo.GetById(loanId);
        if (loan is null)
        {
            return false;
        }

        loan.Close();
        this._loanRepo.Update(loan);
        return true;
    }

    public bool DeleteLoan(Guid loanId)
    {
        foreach (LoanMovement movement in this._movementRepo.GetByLoan(loanId).ToList())
        {
            this._movementRepo.Delete(movement.Id);
        }

        return this._loanRepo.Delete(loanId);
    }

    private LoanMovement CreateMovement(Loan loan, decimal amount, DateTime date, LoanMovementType type)
    {
        Money money = new(amount, loan.Principal.Currency);
        Transaction transaction = new(date, BuildDescription(loan, type), money, MapCategory(loan.Direction, type));
        return new LoanMovement(Guid.NewGuid(), loan.Id, transaction, type);
    }

    /// <summary>
    /// Maps a loan cash movement to the transaction category used for cash-flow bookkeeping:
    /// Borrowed disbursement and Lent repayment are cash in (DEPOSIT); Lent disbursement and
    /// Borrowed repayment are cash out (TRANSFER).
    /// </summary>
    private static TransactionCategory MapCategory(LoanDirection direction, LoanMovementType type) => (direction, type) switch
    {
        (LoanDirection.Borrowed, LoanMovementType.Disbursement) => TransactionCategory.DEPOSIT,
        (LoanDirection.Borrowed, LoanMovementType.Repayment) => TransactionCategory.TRANSFER,
        (LoanDirection.Lent, LoanMovementType.Disbursement) => TransactionCategory.TRANSFER,
        (LoanDirection.Lent, LoanMovementType.Repayment) => TransactionCategory.DEPOSIT,
        _ => throw new ArgumentOutOfRangeException(),
    };

    private static string BuildDescription(Loan loan, LoanMovementType type) => type switch
    {
        LoanMovementType.Disbursement => loan.Direction == LoanDirection.Borrowed
            ? $"Loan from {loan.Counterparty}"
            : $"Loan to {loan.Counterparty}",
        LoanMovementType.Repayment => loan.Direction == LoanDirection.Borrowed
            ? $"Repayment to {loan.Counterparty}"
            : $"Repayment from {loan.Counterparty}",
        _ => throw new ArgumentOutOfRangeException(),
    };
}
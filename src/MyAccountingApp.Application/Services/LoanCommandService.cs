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

    /// <summary>
    /// Maps a loan cash movement to the transaction category used for cash-flow bookkeeping.
    /// Loan movements are real cash in/out: they count inside the Internal totals, but keep their
    /// own category so they are shown as loan movements (not as deposits/transfers) in the UI.
    /// Borrowed disbursement and Lent repayment are cash in (LOAN_IN); Lent disbursement and
    /// Borrowed repayment are cash out (LOAN_OUT).
    /// </summary>
    private static TransactionCategory MapCategory(LoanDirection direction, LoanMovementType type) => (direction, type) switch
    {
        (LoanDirection.Borrowed, LoanMovementType.Disbursement) => TransactionCategory.LOAN_IN,
        (LoanDirection.Borrowed, LoanMovementType.Repayment) => TransactionCategory.LOAN_OUT,
        (LoanDirection.Borrowed, LoanMovementType.Interest) => TransactionCategory.LOAN_OUT,
        (LoanDirection.Lent, LoanMovementType.Disbursement) => TransactionCategory.LOAN_OUT,
        (LoanDirection.Lent, LoanMovementType.Repayment) => TransactionCategory.LOAN_IN,
        (LoanDirection.Lent, LoanMovementType.Interest) => TransactionCategory.LOAN_IN,
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
        LoanMovementType.Interest => loan.Direction == LoanDirection.Borrowed
            ? $"Interest paid to {loan.Counterparty}"
            : $"Interest received from {loan.Counterparty}",
        _ => throw new ArgumentOutOfRangeException(),
    };

    private LoanMovement CreateMovement(Loan loan, decimal amount, DateTime date, LoanMovementType type)
    {
        Money money = new(amount, loan.Principal.Currency);
        Transaction transaction = new(date, BuildDescription(loan, type), money, MapCategory(loan.Direction, type), "Loan");
        return new LoanMovement(Guid.NewGuid(), loan.Id, transaction, type);
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

        return this._loanQuery.GetById(loan.Id) ?? throw new InvalidOperationException("Loan could not be loaded after creation.");
    }

    public LoanSummaryDto AddRepayment(Guid loanId, AddLoanRepaymentRequest request)
    {
        Loan? loan = this._loanRepo.GetById(loanId);
        if (loan is null)
        {
            throw new InvalidOperationException("Loan not found.");
        }

        if (request.Amount < 0)
        {
            throw new ArgumentException("Capital amount must not be negative.", nameof(request.Amount));
        }

        if (request.InterestAmount < 0)
        {
            throw new ArgumentException("Interest amount must not be negative.", nameof(request.InterestAmount));
        }

        if (request.Amount == 0 && request.InterestAmount == 0)
        {
            throw new ArgumentException("Repayment must include capital, interest, or both.", nameof(request.Amount));
        }

        if (request.Amount > 0)
        {
            LoanMovement repayment = this.CreateMovement(loan, request.Amount, request.Date, LoanMovementType.Repayment);
            this._movementRepo.Add(repayment);
        }

        if (request.InterestAmount > 0)
        {
            LoanMovement interest = this.CreateMovement(loan, request.InterestAmount, request.Date, LoanMovementType.Interest);
            this._movementRepo.Add(interest);
        }

        return this._loanQuery.GetById(loanId) ?? throw new InvalidOperationException("Loan could not be loaded after repayment.");
    }

    public LoanSummaryDto UpdateMovement(Guid loanId, Guid movementId, UpdateLoanMovementRequest request)
    {
        Loan? loan = this._loanRepo.GetById(loanId);
        if (loan is null)
        {
            throw new InvalidOperationException("Loan not found.");
        }

        LoanMovement? existing = this._movementRepo.GetByLoan(loanId).FirstOrDefault(m => m.Id == movementId);
        if (existing is null)
        {
            throw new InvalidOperationException("Loan movement not found.");
        }

        if (existing.Type == LoanMovementType.Disbursement)
        {
            throw new ArgumentException("The disbursement movement is bound to the loan principal and cannot be edited.", nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Movement amount must be greater than zero.", nameof(request.Amount));
        }

        if (!Enum.TryParse<LoanMovementType>(request.Type, ignoreCase: true, out LoanMovementType type) || type == LoanMovementType.Disbursement)
        {
            throw new ArgumentException($"Invalid loan movement type: {request.Type}", nameof(request.Type));
        }

        Money money = new(request.Amount, loan.Principal.Currency);
        Transaction transaction = new(request.Date, BuildDescription(loan, type), money, MapCategory(loan.Direction, type), "Loan");
        LoanMovement updated = new(movementId, loanId, transaction, type);
        this._movementRepo.Update(updated);

        return this._loanQuery.GetById(loanId) ?? throw new InvalidOperationException("Loan could not be loaded after movement update.");
    }

    public bool DeleteMovement(Guid loanId, Guid movementId)
    {
        LoanMovement? movement = this._movementRepo.GetByLoan(loanId).FirstOrDefault(m => m.Id == movementId);
        if (movement is null)
        {
            return false;
        }

        if (movement.Type == LoanMovementType.Disbursement)
        {
            throw new ArgumentException("The disbursement movement is bound to the loan principal and cannot be deleted.", nameof(movementId));
        }

        return this._movementRepo.Delete(movementId);
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
}
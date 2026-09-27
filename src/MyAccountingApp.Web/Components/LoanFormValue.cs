using MyAccountingApp.Contracts;

namespace MyAccountingApp.Web.Components;

/// <summary>Values captured by the new-loan dialog when the user confirms.</summary>
public sealed record LoanFormValue(string Counterparty, string Direction, decimal Amount, string Currency, DateTime StartDate, string? Notes);

/// <summary>Repayment split between principal and interest, emitted by the repayment dialog.</summary>
public sealed record LoanRepaymentValue(DateTime Date, decimal Amount, decimal InterestAmount);

/// <summary>Edited loan movement, emitted by the movement edit dialog.</summary>
public sealed record LoanMovementEditValue(Guid LoanId, Guid MovementId, DateTime Date, decimal Amount, string Type);

/// <summary>A loan together with one of its movements, used to address movement actions.</summary>
public sealed record LoanMovementTarget(LoanSummaryDto Loan, LoanMovementDto Movement);

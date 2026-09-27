namespace MyAccountingApp.Contracts;

public sealed record CreateLoanRequest(
    DateTime StartDate,
    string Counterparty,
    decimal Amount,
    string Currency,
    string Direction,
    string? Notes = null);

public sealed record AddLoanRepaymentRequest(
    DateTime Date,
    decimal Amount,
    decimal InterestAmount = 0);

public sealed record UpdateLoanMovementRequest(
    DateTime Date,
    decimal Amount,
    string Type);
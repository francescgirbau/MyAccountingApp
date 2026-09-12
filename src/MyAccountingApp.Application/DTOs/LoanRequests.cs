namespace MyAccountingApp.Application.DTOs;

public sealed record CreateLoanRequest(
    DateTime StartDate,
    string Counterparty,
    decimal Amount,
    string Currency,
    string Direction,
    string? Notes = null);

public sealed record AddLoanRepaymentRequest(
    DateTime Date,
    decimal Amount);
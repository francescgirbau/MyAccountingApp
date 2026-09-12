namespace MyAccountingApp.Application.DTOs;

public sealed record LoanSummaryDto(
    Guid LoanId,
    string Counterparty,
    string Direction,
    decimal Principal,
    string Currency,
    decimal Repaid,
    decimal Outstanding,
    bool IsOverpaid,
    bool IsClosed,
    DateTime StartDate,
    DateTime? LastMovementDate);
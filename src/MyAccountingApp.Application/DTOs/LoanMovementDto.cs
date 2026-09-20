namespace MyAccountingApp.Application.DTOs;

public sealed record LoanMovementDto(
    Guid Id,
    DateTime Date,
    string Type,
    string Description,
    decimal Amount,
    string Currency);
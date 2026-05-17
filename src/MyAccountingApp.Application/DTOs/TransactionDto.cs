namespace MyAccountingApp.Application.DTOs;

public record TransactionDto(
    Guid Id,
    DateTime Date,
    string Description,
    MoneyDto Money,
    string Category);

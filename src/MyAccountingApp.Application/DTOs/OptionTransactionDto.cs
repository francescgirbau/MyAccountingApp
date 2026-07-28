namespace MyAccountingApp.Application.DTOs;

public record OptionTransactionDto(
    Guid Id,
    DateTime Date,
    string Description,
    string Symbol,
    string Isin,
    decimal Quantity,
    MoneyDto Premium,
    string Type);

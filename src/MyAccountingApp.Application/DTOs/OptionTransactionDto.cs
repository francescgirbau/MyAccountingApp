namespace MyAccountingApp.Application.DTOs;

public record OptionTransactionDto(
    TransactionDto Transaction,
    string Symbol,
    string Isin,
    decimal Quantity,
    string Type);

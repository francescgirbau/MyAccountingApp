namespace MyAccountingApp.Application.DTOs;

public record AssetTransactionDto(
    TransactionDto Transaction,
    string Symbol,
    decimal Quantity,
    string Type,
    MoneyDto UnitaryCost);

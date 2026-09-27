namespace MyAccountingApp.Contracts;

public record AssetTransactionDto(
    TransactionDto Transaction,
    string Symbol,
    decimal Quantity,
    string Type,
    MoneyDto UnitaryCost,
    string? Source = null);
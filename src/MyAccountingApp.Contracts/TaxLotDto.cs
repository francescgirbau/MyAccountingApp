namespace MyAccountingApp.Contracts;

public record TaxLotDto(
    DateTime PurchaseDate,
    decimal Quantity,
    decimal UnitaryCost,
    decimal TotalCost);
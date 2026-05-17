namespace MyAccountingApp.Application.DTOs;

public record TaxLotDto(
    DateTime PurchaseDate,
    decimal Quantity,
    decimal UnitaryCost,
    decimal TotalCost);

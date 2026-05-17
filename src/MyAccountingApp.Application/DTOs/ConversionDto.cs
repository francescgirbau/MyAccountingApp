namespace MyAccountingApp.Application.DTOs;

public record ConversionDto(
    DateTime Date,
    string Source,
    Dictionary<string, decimal> Quotes);

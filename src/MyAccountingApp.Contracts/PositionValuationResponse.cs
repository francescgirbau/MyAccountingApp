namespace MyAccountingApp.Contracts;

public sealed record PositionValuationResponse(
    DateOnly AsOf,
    IReadOnlyList<PositionValuationDto> Positions);
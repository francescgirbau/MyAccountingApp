namespace MyAccountingApp.Contracts;

public record TransactionDto(
    Guid Id,
    DateTime Date,
    string Description,
    MoneyDto Money,
    string Category,
    string? Source = null,
    Guid? FxPairId = null,
    string? FxLeg = null,
    decimal? FxBrokerRate = null,
    string? FxExternalKey = null,
    bool NeedsReview = false);
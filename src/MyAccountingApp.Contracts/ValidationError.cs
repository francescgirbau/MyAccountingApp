namespace MyAccountingApp.Contracts;

public record ValidationError(
    string Field,
    string Message,
    string Severity,
    string EntityType = "Transaction",
    IReadOnlyList<Guid>? EntityIds = null,
    string? Symbol = null,
    DateOnly? Date = null,
    string? DeepLink = null);
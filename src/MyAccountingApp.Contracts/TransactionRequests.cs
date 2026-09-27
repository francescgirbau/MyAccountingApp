namespace MyAccountingApp.Contracts;

public record CreateTransactionRequest(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category);

public record CreateFxTransactionRequest(
    DateTime Date,
    string FromCurrency,
    decimal FromAmount,
    string ToCurrency,
    decimal ToAmount,
    decimal? Rate = null,
    string? Description = null);

public record CreateAssetTransactionRequest(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    string Symbol,
    decimal Quantity,
    string Type);

public record SplitAdjustmentRequest(
    string Symbol,
    decimal Factor,
    DateTime? AsOfDate = null);

public record UpdateOptionTransactionRequest(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    string Symbol,
    string Isin,
    decimal Quantity,
    string Type);
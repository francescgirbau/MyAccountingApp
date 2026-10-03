namespace MyAccountingApp.Contracts;

/// <summary>
/// Read-only diagnostic of the data imported from Interactive Brokers (IBKR) statements:
/// how much of the store comes from IBKR files, per year and per source file, plus the
/// option contracts the tracker reports as open even though their expiration is in the
/// past (missing closing leg produced by legacy imports) and candidate phantom asset rows.
/// </summary>
public sealed record IBKRDataHealthDto(
    int TotalTransactions,
    int TotalAssets,
    int TotalOptions,
    int IbkrTransactions,
    int IbkrAssets,
    int IbkrOptions,
    IReadOnlyList<IBKRYearSummaryDto> Years,
    IReadOnlyList<IBKRSourcesSummaryDto> Sources,
    IReadOnlyList<IBKROpenExpiredContractDto> OpenExpiredContracts,
    IReadOnlyList<IBKRPhantomAssetDto> PhantomAssets);

/// <summary>
/// Counts for one year, split between "all data" and "IBKR statement data".
/// </summary>
public sealed record IBKRYearSummaryDto(
    int Year,
    int Transactions,
    int Assets,
    int Options,
    int IbkrTransactions,
    int IbkrAssets,
    int IbkrOptions);

/// <summary>
/// Counts per imported source file (the CSV provenance recorded on each row).
/// </summary>
public sealed record IBKRSourcesSummaryDto(
    string Source,
    int Transactions,
    int Assets,
    int Options);

/// <summary>
/// An option contract whose expiration is in the past but that the tracker reports as open:
/// the imported data has no closing leg for it (legacy imports dropped zero-premium legs).
/// </summary>
public sealed record IBKROpenExpiredContractDto(
    string Description,
    string Strategy,
    string Side,
    decimal Quantity,
    DateOnly Expiration,
    DateOnly OpenDate,
    int Legs);

/// <summary>
/// A candidate phantom asset row: a quantity of one that is not an investment/divestment,
/// typically produced by legacy imports that turned option or cash rows into stock rows.
/// </summary>
public sealed record IBKRPhantomAssetDto(
    DateTime Date,
    string Symbol,
    decimal Quantity,
    string Category,
    decimal Amount,
    string Currency,
    string? Source);
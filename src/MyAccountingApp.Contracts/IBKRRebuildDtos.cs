namespace MyAccountingApp.Contracts;

/// <summary>
/// Request for the IBKR rebuild: the calendar years whose Interactive Brokers rows
/// should be removed so they can be re-imported cleanly.
/// </summary>
public sealed record IBKRRebuildRequest(int[] Years);

/// <summary>
/// Preview of what an IBKR rebuild would delete for the requested years.
/// Non-IBKR sources and manual entries are never part of a rebuild; option rows
/// without a recorded source in the selected years are included because options
/// come from IBKR statements in this app.
/// </summary>
public sealed record IBKRRebuildPreviewDto(
    int Transactions,
    int Assets,
    int Options,
    int OptionsWithoutSource,
    IReadOnlyList<IBKRRebuildYearDto> Years);

/// <summary>Per-year counts of the rows an IBKR rebuild would delete.</summary>
public sealed record IBKRRebuildYearDto(
    int Year,
    int Transactions,
    int Assets,
    int Options,
    int OptionsWithoutSource);

/// <summary>Result of an executed IBKR rebuild.</summary>
public sealed record IBKRRebuildResultDto(
    string BackupFile,
    int TransactionsDeleted,
    int AssetsDeleted,
    int OptionsDeleted,
    int OptionsWithoutSourceDeleted,
    IReadOnlyList<IBKRRebuildYearDto> Years);
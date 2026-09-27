namespace MyAccountingApp.Application.DTOs;

public sealed record AssetTransactionPatch(string? Symbol);

public sealed record OptionTransactionPatch(string? Symbol);

public sealed record TransactionPatch(string? Category);

public sealed record BatchPatchFailure(Guid Id, string Error);

public sealed record BatchPatchResult(
    int Requested,
    int Updated,
    IReadOnlyList<BatchPatchFailure> Failures);

public sealed record BatchDeleteResult(
    int Requested,
    int Deleted,
    IReadOnlyList<BatchPatchFailure> Failures);

public sealed record SplitAdjustmentPreviewItem(
    Guid Id,
    DateTime Date,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal UnitaryCostBefore,
    decimal UnitaryCostAfter,
    decimal Amount,
    string Currency);

public sealed record SplitAdjustmentPreview(
    string Symbol,
    decimal Factor,
    DateTime? AsOfDate,
    IReadOnlyList<SplitAdjustmentPreviewItem> Items);

public sealed record SplitAdjustmentResult(
    int Requested,
    int Updated,
    IReadOnlyList<BatchPatchFailure> Failures);

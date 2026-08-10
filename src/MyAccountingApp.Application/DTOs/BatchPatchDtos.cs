namespace MyAccountingApp.Application.DTOs;

public sealed record AssetTransactionPatch(string? Symbol);

public sealed record BatchPatchFailure(Guid Id, string Error);

public sealed record BatchPatchResult(
    int Requested,
    int Updated,
    IReadOnlyList<BatchPatchFailure> Failures);

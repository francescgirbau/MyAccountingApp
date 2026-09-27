namespace MyAccountingApp.Contracts;

public record BatchAssetTransactionPatchRequest(List<Guid> Ids, AssetTransactionPatch Patch);

public record BatchOptionTransactionPatchRequest(List<Guid> Ids, OptionTransactionPatch Patch);

public record BatchTransactionPatchRequest(List<Guid> Ids, TransactionPatch Patch);

public record BulkDeleteRequest(List<Guid> Ids);
using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IAssetTransactionCommandService
{
    BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, AssetTransactionPatch patch);

    BatchDeleteResult DeleteMany(IReadOnlyList<Guid> ids);

    SplitAdjustmentPreview PreviewSplit(string symbol, decimal factor, DateTime? asOfDate);

    SplitAdjustmentResult ApplySplit(string symbol, decimal factor, DateTime? asOfDate);
}

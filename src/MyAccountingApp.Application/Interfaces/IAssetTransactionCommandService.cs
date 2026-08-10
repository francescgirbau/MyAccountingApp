using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IAssetTransactionCommandService
{
    BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, AssetTransactionPatch patch);
}

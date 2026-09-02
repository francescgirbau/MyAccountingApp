using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface ITransactionCommandService
{
    BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, TransactionPatch patch);

    BatchDeleteResult DeleteMany(IReadOnlyList<Guid> ids);
}
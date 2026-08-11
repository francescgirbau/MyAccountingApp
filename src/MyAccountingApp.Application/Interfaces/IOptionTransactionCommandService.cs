using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IOptionTransactionCommandService
{
    BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, OptionTransactionPatch patch);
}
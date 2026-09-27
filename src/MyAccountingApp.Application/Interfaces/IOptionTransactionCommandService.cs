using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface IOptionTransactionCommandService
{
    BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, OptionTransactionPatch patch);
}
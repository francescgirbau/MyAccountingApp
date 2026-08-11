using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public sealed class OptionTransactionCommandService : IOptionTransactionCommandService
{
    private readonly IOptionTransactionRepository _repository;

    public OptionTransactionCommandService(IOptionTransactionRepository repository)
    {
        this._repository = repository;
    }

    public BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, OptionTransactionPatch patch)
    {
        List<OptionTransaction> all = this._repository.GetAll().ToList();
        List<BatchPatchFailure> failures = new();
        int updated = 0;

        foreach (Guid id in ids)
        {
            OptionTransaction? target = all.FirstOrDefault(t => t.Transaction.Id == id);
            if (target is null)
            {
                failures.Add(new BatchPatchFailure(id, "Option transaction not found."));
                continue;
            }

            try
            {
                if (patch.Symbol is not null)
                {
                    target.UpdateSymbol(patch.Symbol);
                }

                updated++;
            }
            catch (ArgumentException ex)
            {
                failures.Add(new BatchPatchFailure(id, ex.Message));
            }
        }

        if (updated > 0)
        {
            this._repository.Initialize(all);
        }

        return new BatchPatchResult(ids.Count, updated, failures);
    }
}
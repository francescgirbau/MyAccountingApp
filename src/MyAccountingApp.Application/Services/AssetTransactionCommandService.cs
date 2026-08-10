using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public sealed class AssetTransactionCommandService : IAssetTransactionCommandService
{
    private readonly IPortfolioRepository _repository;

    public AssetTransactionCommandService(IPortfolioRepository repository)
    {
        this._repository = repository;
    }

    public BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, AssetTransactionPatch patch)
    {
        List<AssetTransaction> all = this._repository.GetAllTransactions().ToList();
        List<BatchPatchFailure> failures = new();
        int updated = 0;

        foreach (Guid id in ids)
        {
            AssetTransaction? target = all.FirstOrDefault(t => t.Transaction.Id == id);
            if (target is null)
            {
                failures.Add(new BatchPatchFailure(id, "Asset transaction not found."));
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

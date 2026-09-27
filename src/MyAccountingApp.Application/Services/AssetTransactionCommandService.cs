using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
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
        List<Guid> distinctIds = ids.Distinct().ToList();
        List<AssetTransaction> all = this._repository.GetAllTransactions().ToList();
        List<BatchPatchFailure> failures = new();
        int updated = 0;

        foreach (Guid id in distinctIds)
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
                    string before = target.Symbol;
                    target.UpdateSymbol(patch.Symbol);
                    if (!string.Equals(before, target.Symbol, StringComparison.Ordinal))
                    {
                        updated++;
                    }
                }
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

        return new BatchPatchResult(distinctIds.Count, updated, failures);
    }

    public BatchDeleteResult DeleteMany(IReadOnlyList<Guid> ids)
    {
        List<Guid> distinctIds = ids.Distinct().ToList();
        List<AssetTransaction> all = this._repository.GetAllTransactions().ToList();
        List<BatchPatchFailure> failures = new();
        int deleted = 0;

        foreach (Guid id in distinctIds)
        {
            AssetTransaction? target = all.FirstOrDefault(t => t.Transaction.Id == id);
            if (target is null)
            {
                failures.Add(new BatchPatchFailure(id, "Asset transaction not found."));
                continue;
            }

            all.Remove(target);
            deleted++;
        }

        if (deleted > 0)
        {
            this._repository.Initialize(all);
        }

        return new BatchDeleteResult(distinctIds.Count, deleted, failures);
    }

    public SplitAdjustmentPreview PreviewSplit(string symbol, decimal factor, DateTime? asOfDate)
    {
        if (factor <= 0)
        {
            throw new ArgumentException("Split factor must be greater than zero.");
        }

        List<AssetTransaction> targets = this.GetSplitTargets(symbol, asOfDate);
        List<SplitAdjustmentPreviewItem> items = targets
            .Select(t =>
            {
                decimal after = NewQuantity(t.Quantity, factor);
                return new SplitAdjustmentPreviewItem(
                    t.Transaction.Id,
                    t.Transaction.Date,
                    t.Quantity,
                    after,
                    t.UnitaryCost().Amount,
                    after > 0 ? t.UnitaryCost().Amount * t.Quantity / after : 0,
                    t.Transaction.Money.Amount,
                    t.Transaction.Money.Currency);
            })
            .ToList();

        return new SplitAdjustmentPreview(symbol, factor, asOfDate, items);
    }

    public SplitAdjustmentResult ApplySplit(string symbol, decimal factor, DateTime? asOfDate)
    {
        if (factor <= 0)
        {
            throw new ArgumentException("Split factor must be greater than zero.");
        }

        List<AssetTransaction> all = this._repository.GetAllTransactions().ToList();
        List<AssetTransaction> targets = all.Where(IsSplitTarget(symbol, asOfDate)).ToList();
        List<BatchPatchFailure> failures = new();
        int updated = 0;

        foreach (AssetTransaction target in targets)
        {
            try
            {
                decimal before = target.Quantity;
                target.ApplySplitFactor(factor);
                if (target.Quantity != before)
                {
                    updated++;
                }
            }
            catch (ArgumentException ex)
            {
                failures.Add(new BatchPatchFailure(target.Transaction.Id, ex.Message));
            }
        }

        if (updated > 0)
        {
            this._repository.Initialize(all);
        }

        return new SplitAdjustmentResult(targets.Count, updated, failures);
    }

    private static Func<AssetTransaction, bool> IsSplitTarget(string symbol, DateTime? asOfDate) =>
        t => t.Symbol == symbol && (asOfDate is null || t.Transaction.Date.Date <= asOfDate.Value.Date);

    private static decimal NewQuantity(decimal quantity, decimal factor) =>
        Math.Round(quantity * factor, 0, MidpointRounding.AwayFromZero);

    private List<AssetTransaction> GetSplitTargets(string symbol, DateTime? asOfDate)
    {
        return this._repository.GetAllTransactions()
            .Where(IsSplitTarget(symbol, asOfDate))
            .OrderBy(t => t.Transaction.Date)
            .ToList();
    }
}

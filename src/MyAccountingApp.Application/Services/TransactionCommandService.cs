using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public sealed class TransactionCommandService : ITransactionCommandService
{
    private readonly ITransactionRepository _repository;

    public TransactionCommandService(ITransactionRepository repository)
    {
        this._repository = repository;
    }

    public BatchPatchResult PatchMany(IReadOnlyList<Guid> ids, TransactionPatch patch)
    {
        List<Guid> distinctIds = ids.Distinct().ToList();
        List<Transaction> all = this._repository.GetAll().ToList();
        List<BatchPatchFailure> failures = new();
        int updated = 0;

        foreach (Guid id in distinctIds)
        {
            Transaction? target = all.FirstOrDefault(t => t.Id == id);
            if (target is null)
            {
                failures.Add(new BatchPatchFailure(id, "Transaction not found."));
                continue;
            }

            try
            {
                if (patch.Category is not null)
                {
                    if (!Enum.TryParse<TransactionCategory>(patch.Category, ignoreCase: true, out TransactionCategory category)
                        || !Enum.IsDefined(category))
                    {
                        throw new ArgumentException($"Invalid category '{patch.Category}'.");
                    }

                    TransactionCategory before = target.Category;
                    target.UpdateCategory(category);
                    if (before != target.Category)
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
}
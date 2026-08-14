using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class TransferMatchingService : ITransferMatchingService
{
    private readonly ITransactionRepository _transactionRepo;

    public TransferMatchingService(ITransactionRepository transactionRepo)
    {
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
    }

    public TransferMatchingResult Recalculate()
    {
        DateTime calculatedAtUtc = DateTime.UtcNow;
        List<Transaction> all = this._transactionRepo.GetAll().ToList();
        IReadOnlyList<(Guid TransferId, Guid DepositId)> pairs = TransferDepositPairing.Pair(all);
        int transferCount = all.Count(t => t.Category == TransactionCategory.TRANSFER);

        return new TransferMatchingResult(
            transferCount,
            pairs.Count,
            transferCount - pairs.Count,
            0,
            calculatedAtUtc);
    }
}
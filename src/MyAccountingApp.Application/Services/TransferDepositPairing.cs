using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

public static class TransferDepositPairing
{
    private const int MaxDaysBetweenTransfers = 3;

    public static bool IsValidPair(Transaction transfer, Transaction deposit)
    {
        return transfer.Category == TransactionCategory.TRANSFER &&
               deposit.Category == TransactionCategory.DEPOSIT &&
               transfer.Money.Amount == deposit.Money.Amount &&
               transfer.Money.Currency == deposit.Money.Currency &&
               Math.Abs((deposit.Date - transfer.Date).TotalDays) <= MaxDaysBetweenTransfers &&
               transfer.Id != deposit.Id;
    }

    public static IReadOnlyList<(Guid TransferId, Guid DepositId)> Pair(IEnumerable<Transaction> transactions)
    {
        List<Transaction> all = transactions.ToList();
        List<Transaction> transfers = all
            .Where(t => t.Category == TransactionCategory.TRANSFER)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();
        List<Transaction> deposits = all
            .Where(t => t.Category == TransactionCategory.DEPOSIT)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        HashSet<Guid> usedDeposits = new HashSet<Guid>();
        List<(Guid TransferId, Guid DepositId)> pairs = new List<(Guid TransferId, Guid DepositId)>();

        foreach (Transaction transfer in transfers)
        {
            Transaction? deposit = deposits.FirstOrDefault(d =>
                !usedDeposits.Contains(d.Id) && IsValidPair(transfer, d));

            if (deposit != null)
            {
                usedDeposits.Add(deposit.Id);
                pairs.Add((transfer.Id, deposit.Id));
            }
        }

        return pairs;
    }

    public static IReadOnlyList<Guid> UnmatchedTransferIds(IEnumerable<Transaction> transactions)
    {
        HashSet<Guid> pairedTransferIds = new HashSet<Guid>(Pair(transactions).Select(p => p.TransferId));
        return transactions
            .Where(t => t.Category == TransactionCategory.TRANSFER && !pairedTransferIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class TransferMatchingService : ITransferMatchingService
{
    private const int MaxDaysBetweenTransfers = 3;

    private readonly ITransactionRepository _transactionRepo;

    public TransferMatchingService(ITransactionRepository transactionRepo)
    {
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
    }

    public TransferMatchingResult Recalculate()
    {
        DateTime calculatedAtUtc = DateTime.UtcNow;
        List<Transaction> all = this._transactionRepo.GetAll().ToList();
        List<Transaction> transfers = all
            .Where(t => BankCsvImportService.IsTransfer(t.Description))
            .ToList();

        List<Transaction> expenses = transfers
            .Where(t => t.Money.Amount > 0 && t.Category != TransactionCategory.TRANSFER)
            .ToList();

        List<Transaction> incomes = transfers
            .Where(t => t.Money.Amount > 0 && t.Category != TransactionCategory.TRANSFER)
            .ToList();

        int matchedPairs = 0;
        int changedTransactions = 0;

        foreach (Transaction expense in expenses)
        {
            if (expense.Category != TransactionCategory.EXPENSE)
            {
                continue;
            }

            Transaction? match = incomes.FirstOrDefault(inc =>
                inc.Category == TransactionCategory.INCOME &&
                inc.Money.Amount == expense.Money.Amount &&
                inc.Money.Currency == expense.Money.Currency &&
                Math.Abs((inc.Date - expense.Date).TotalDays) <= MaxDaysBetweenTransfers &&
                inc.Id != expense.Id);

            if (match == null)
            {
                continue;
            }

            ReplaceCategoryInList(all, expense, TransactionCategory.TRANSFER);
            ReplaceCategoryInList(all, match, TransactionCategory.TRANSFER);
            matchedPairs++;
            changedTransactions += 2;
        }

        if (changedTransactions > 0)
        {
            this._transactionRepo.Initialize(all);
        }

        int unmatchedTransfers = all.Count(t =>
            BankCsvImportService.IsTransfer(t.Description) &&
            t.Category != TransactionCategory.TRANSFER);

        return new TransferMatchingResult(
            transfers.Count,
            matchedPairs,
            unmatchedTransfers,
            changedTransactions,
            calculatedAtUtc);
    }

    private static void ReplaceCategoryInList(List<Transaction> all, Transaction transaction, TransactionCategory newCategory)
    {
        Transaction updated = new Transaction(
            transaction.Id,
            transaction.Date,
            transaction.Description,
            transaction.Money,
            newCategory,
            transaction.Source);

        int index = all.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
        {
            all[index] = updated;
        }
    }
}
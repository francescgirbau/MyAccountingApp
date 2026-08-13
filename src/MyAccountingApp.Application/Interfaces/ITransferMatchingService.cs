using System;

namespace MyAccountingApp.Application.Interfaces;

public record TransferMatchingResult(
    int TransferCount,
    int MatchedPairs,
    int UnmatchedTransfers,
    int ChangedTransactions,
    DateTime CalculatedAtUtc);

public interface ITransferMatchingService
{
    TransferMatchingResult Recalculate();
}
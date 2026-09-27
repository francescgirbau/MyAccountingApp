namespace MyAccountingApp.Contracts;

public record TransferMatchingResult(
    int TransferCount,
    int MatchedPairs,
    int UnmatchedTransfers,
    int ChangedTransactions,
    DateTime CalculatedAtUtc);
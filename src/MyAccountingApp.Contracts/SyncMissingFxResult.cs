namespace MyAccountingApp.Contracts;

public sealed record SyncMissingFxResult(
    int RequestedDates,
    int SyncedDates);
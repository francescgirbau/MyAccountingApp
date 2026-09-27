namespace MyAccountingApp.Contracts;

public sealed record AuthStatusDto(
    bool IsEnabled = false,
    bool IsInitialized = false,
    bool IsUnlocked = false);
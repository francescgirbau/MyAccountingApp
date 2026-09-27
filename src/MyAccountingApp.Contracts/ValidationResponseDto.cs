namespace MyAccountingApp.Contracts;

public sealed record ValidationResponseDto(
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationError> Warnings);
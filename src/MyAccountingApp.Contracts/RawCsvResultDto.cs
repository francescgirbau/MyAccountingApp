namespace MyAccountingApp.Contracts;

public sealed record RawCsvResultDto(
    int Imported,
    int Skipped,
    List<string>? Errors);
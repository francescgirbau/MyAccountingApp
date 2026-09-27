namespace MyAccountingApp.Contracts;

public sealed record ResetResultDto(
    string? Message,
    int ClearedTransactions,
    int ClearedAssetTransactions,
    int ClearedOptionTransactions);

public sealed record DeleteYearResultDto(
    int Year,
    int DeletedTransactions,
    int DeletedAssets,
    int DeletedOptions);

public sealed record DeleteAssetYearResultDto(
    int Year,
    int DeletedAssets);

public sealed record DeleteYearCountDto(
    int Year,
    int Transactions,
    int Assets,
    int Options);

public sealed record DeleteAssetYearCountDto(
    int Year,
    int Assets);
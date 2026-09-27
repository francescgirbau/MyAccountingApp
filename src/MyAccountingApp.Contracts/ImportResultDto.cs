namespace MyAccountingApp.Contracts;

public record ImportResultDto(
    List<TransactionDto> Transactions,
    List<AssetTransactionDto> AssetTransactions,
    List<OptionTransactionDto> OptionTransactions,
    List<string> Errors,
    List<ValidationError> ValidationErrors,
    List<ValidationError> ValidationWarnings,
    int FilesProcessed,
    int SkippedAssetTransactions);
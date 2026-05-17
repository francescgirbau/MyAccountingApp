using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Application.DTOs;

public record ImportResultDto(
    List<TransactionDto> Transactions,
    List<AssetTransactionDto> AssetTransactions,
    List<string> Errors,
    List<ValidationError> ValidationErrors,
    List<ValidationError> ValidationWarnings,
    int FilesProcessed);

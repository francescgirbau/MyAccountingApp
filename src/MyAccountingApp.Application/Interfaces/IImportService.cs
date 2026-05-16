using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

public interface IImportService
{
    Task<ImportResult> ImportFromFoldersAsync(IEnumerable<string> folderPaths);
}

public class ImportResult
{
    public List<Transaction> Transactions { get; init; } = new();
    public List<AssetTransaction> AssetTransactions { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<ValidationError> ValidationErrors { get; init; } = new();
    public List<ValidationError> ValidationWarnings { get; init; } = new();
    public int FilesProcessed { get; set; }
}

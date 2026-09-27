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
    public List<OptionTransaction> OptionTransactions { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<ValidationError> ValidationErrors { get; init; } = new();
    public List<ValidationError> ValidationWarnings { get; init; } = new();
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Gets the number of asset transactions skipped because a row with the same content
    /// fingerprint was already present (i.e. the same file was imported before).
    /// </summary>
    public int SkippedAssetTransactions { get; set; }
}

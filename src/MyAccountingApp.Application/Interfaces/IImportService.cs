using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

public interface IImportService
{
    /// <summary>
    /// Parses, validates and deduplicates the CSV files of the given folders.
    /// When <paramref name="dryRun"/> is true, nothing is persisted: the result
    /// describes what an import would do, so the caller can preview it first.
    /// </summary>
    Task<ImportResult> ImportFromFoldersAsync(IEnumerable<string> folderPaths, bool dryRun = false);
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

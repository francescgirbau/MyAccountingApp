using System.Linq;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class ImportService : IImportService
{
    private readonly IBrokerImportService _broker;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;
    private readonly ITransactionValidator _validator;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IBrokerImportService broker,
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo,
        ITransactionValidator validator,
        ILogger<ImportService> logger)
    {
        this._broker = broker ?? throw new ArgumentNullException(nameof(broker));
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this._portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
        this._optionRepo = optionRepo ?? throw new ArgumentNullException(nameof(optionRepo));
        this._validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportResult> ImportFromFoldersAsync(IEnumerable<string> folderPaths)
    {
        ImportResult result = new ImportResult();

        // Batch in memory, then persist once. Per-row AddOrUpdate rewrites the full JSON file
        // each time and times out HttpClient (~100s) on ~2k-row imports.
        List<Domain.Entities.Transaction> pendingTransactions = new List<Domain.Entities.Transaction>();
        List<Domain.Entities.AssetTransaction> pendingAssets = new List<Domain.Entities.AssetTransaction>();
        List<Domain.Entities.OptionTransaction> pendingOptions = new List<Domain.Entities.OptionTransaction>();

        foreach (string folderPath in folderPaths)
        {
            if (!Directory.Exists(folderPath))
            {
                result.Errors.Add($"Folder not found: {folderPath}");
                this._logger.LogWarning("Folder not found: {FolderPath}", folderPath);
                continue;
            }

            string[] csvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.AllDirectories);

            foreach (string csvFile in csvFiles)
            {
                try
                {
                    this._logger.LogInformation("Processing: {CsvFile}", csvFile);
                    string source = Path.GetFileName(csvFile);

                    if (folderPath.Contains("CORPORATE", StringComparison.OrdinalIgnoreCase))
                    {
                        IEnumerable<Domain.Entities.AssetTransaction> corporateTransactions =
                            await this._broker.ParseCorporateActionsAsync(csvFile);
                        foreach (Domain.Entities.AssetTransaction tx in corporateTransactions)
                        {
                            tx.SetSource(source);
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                pendingAssets.Add(tx);
                            }
                        }

                        result.AssetTransactions.AddRange(corporateTransactions);
                    }
                    else
                    {
                        (IEnumerable<Domain.Entities.Transaction> transactions, IEnumerable<Domain.Entities.AssetTransaction> assetTransactions, IEnumerable<Domain.Entities.OptionTransaction> optionTransactions) =
                            await this._broker.ParseAllAsync(csvFile);

                        foreach (Domain.Entities.Transaction tx in transactions)
                        {
                            tx.SetSource(source);
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                pendingTransactions.Add(tx);
                            }
                        }

                        foreach (Domain.Entities.AssetTransaction tx in assetTransactions)
                        {
                            tx.SetSource(source);
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                pendingAssets.Add(tx);
                            }
                        }

                        foreach (Domain.Entities.OptionTransaction tx in optionTransactions)
                        {
                            pendingOptions.Add(tx);
                        }

                        result.Transactions.AddRange(transactions);
                        result.AssetTransactions.AddRange(assetTransactions);
                        result.OptionTransactions.AddRange(optionTransactions);
                    }

                    result.FilesProcessed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error processing {csvFile}: {ex.Message}");
                    this._logger.LogError(ex, "Failed to process {CsvFile}", csvFile);
                }
            }
        }

        if (pendingTransactions.Count > 0)
        {
            List<Domain.Entities.Transaction> merged = this._transactionRepo.GetAll().ToList();
            merged.AddRange(pendingTransactions);
            this._transactionRepo.Initialize(merged);
        }

        if (pendingAssets.Count > 0)
        {
            List<Domain.Entities.AssetTransaction> mergedAssets = this._portfolioRepo.GetAllTransactions().ToList();
            Dictionary<Guid, int> assetIndexById = new Dictionary<Guid, int>(mergedAssets.Count);
            for (int i = 0; i < mergedAssets.Count; i++)
            {
                assetIndexById[mergedAssets[i].Transaction.Id] = i;
            }

            foreach (Domain.Entities.AssetTransaction tx in pendingAssets)
            {
                Guid id = tx.Transaction.Id;
                if (assetIndexById.TryGetValue(id, out int index))
                {
                    mergedAssets[index] = tx;
                }
                else
                {
                    assetIndexById[id] = mergedAssets.Count;
                    mergedAssets.Add(tx);
                }
            }

            this._portfolioRepo.Initialize(mergedAssets);
        }

        if (pendingOptions.Count > 0)
        {
            List<Domain.Entities.OptionTransaction> mergedOptions = this._optionRepo.GetAll().ToList();
            mergedOptions.AddRange(pendingOptions);
            this._optionRepo.Initialize(mergedOptions);
        }

        return result;
    }

    private static int DeduplicateByFingerprint(
        List<Domain.Entities.Transaction> candidates,
        out List<Domain.Entities.Transaction> deduped)
    {
        HashSet<TransactionFingerprint> seen = new HashSet<TransactionFingerprint>();
        deduped = new List<Domain.Entities.Transaction>(candidates.Count);
        int skipped = 0;

        foreach (Domain.Entities.Transaction tx in candidates)
        {
            if (seen.Add(tx.GetFingerprint()))
            {
                deduped.Add(tx);
            }
            else
            {
                skipped++;
            }
        }

        return skipped;
    }
}

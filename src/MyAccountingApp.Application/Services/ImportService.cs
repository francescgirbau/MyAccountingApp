using System.Linq;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class ImportService : IImportService
{
    private readonly IBrokerImportService _broker;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITransactionValidator _validator;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IBrokerImportService broker,
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        ITransactionValidator validator,
        ILogger<ImportService> logger)
    {
        this._broker = broker ?? throw new ArgumentNullException(nameof(broker));
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this._portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
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

                    if (folderPath.Contains("CORPORATE", StringComparison.OrdinalIgnoreCase))
                    {
                        IEnumerable<Domain.Entities.AssetTransaction> corporateTransactions =
                            await this._broker.ParseCorporateActionsAsync(csvFile);
                        foreach (Domain.Entities.AssetTransaction tx in corporateTransactions)
                        {
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
                        (IEnumerable<Domain.Entities.Transaction> transactions, IEnumerable<Domain.Entities.AssetTransaction> assetTransactions) =
                            await this._broker.ParseAllAsync(csvFile);

                        foreach (Domain.Entities.Transaction tx in transactions)
                        {
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
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                pendingAssets.Add(tx);
                            }
                        }

                        result.Transactions.AddRange(transactions);
                        result.AssetTransactions.AddRange(assetTransactions);
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

        int matchedPairs = this.MatchTransferPairs();

        this._logger.LogInformation(
            "Import completed: {FilesProcessed} files, {Transactions} transactions, {AssetTransactions} asset transactions, {Matches} transfer pairs matched, {Errors} errors",
            result.FilesProcessed,
            result.Transactions.Count,
            result.AssetTransactions.Count,
            matchedPairs,
            result.Errors.Count);

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

    private int MatchTransferPairs()
    {
        List<Domain.Entities.Transaction> all = this._transactionRepo.GetAll().ToList();
        List<Domain.Entities.Transaction> transfers = all
            .Where(t => BankCsvImportService.IsTransfer(t.Description))
            .ToList();

        List<Domain.Entities.Transaction> expenses = transfers
            .Where(t => t.Money.Amount > 0 && t.Category != Domain.Enums.TransactionCategory.TRANSFER)
            .ToList();

        List<Domain.Entities.Transaction> incomes = transfers
            .Where(t => t.Money.Amount > 0 && t.Category != Domain.Enums.TransactionCategory.TRANSFER)
            .ToList();

        int matched = 0;
        bool changed = false;

        foreach (Domain.Entities.Transaction expense in expenses)
        {
            if (expense.Category != Domain.Enums.TransactionCategory.EXPENSE)
            {
                continue;
            }

            Domain.Entities.Transaction? match = incomes.FirstOrDefault(inc =>
                inc.Category == Domain.Enums.TransactionCategory.INCOME &&
                inc.Money.Amount == expense.Money.Amount &&
                inc.Money.Currency == expense.Money.Currency &&
                Math.Abs((inc.Date - expense.Date).TotalDays) <= 3 &&
                inc.Id != expense.Id);

            if (match != null)
            {
                this.ReplaceCategoryInList(all, expense, Domain.Enums.TransactionCategory.TRANSFER);
                this.ReplaceCategoryInList(all, match, Domain.Enums.TransactionCategory.TRANSFER);
                matched++;
                changed = true;
            }
        }

        if (changed)
        {
            this._transactionRepo.Initialize(all);
        }

        return matched;
    }

    private void ReplaceCategoryInList(
        List<Domain.Entities.Transaction> all,
        Domain.Entities.Transaction transaction,
        Domain.Enums.TransactionCategory newCategory)
    {
        Domain.Entities.Transaction updated = new Domain.Entities.Transaction(
            transaction.Id,
            transaction.Date,
            transaction.Description,
            transaction.Money,
            newCategory);

        int index = all.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
        {
            all[index] = updated;
        }
    }
}

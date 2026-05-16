using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class ImportService : IImportService
{
    private readonly IAgent _agent;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITransactionValidator _validator;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IAgent agent,
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        ITransactionValidator validator,
        ILogger<ImportService> logger)
    {
        this._agent = agent ?? throw new ArgumentNullException(nameof(agent));
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this._portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
        this._validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportResult> ImportFromFoldersAsync(IEnumerable<string> folderPaths)
    {
        ImportResult result = new ImportResult();

        foreach (string folderPath in folderPaths)
        {
            if (!Directory.Exists(folderPath))
            {
                result.Errors.Add($"Folder not found: {folderPath}");
                this._logger.LogWarning("Folder not found: {FolderPath}", folderPath);
                continue;
            }

            string[] csvFiles = Directory.GetFiles(folderPath, "*.csv");

            foreach (string csvFile in csvFiles)
            {
                try
                {
                    this._logger.LogInformation("Processing: {CsvFile}", csvFile);

                    if (folderPath.Contains("CORPORATE", StringComparison.OrdinalIgnoreCase))
                    {
                        IEnumerable<AssetTransaction> corporateTransactions =
                            await this._agent.ParseCorporateActionsAsync(csvFile);
                        foreach (AssetTransaction tx in corporateTransactions)
                        {
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                this._portfolioRepo.AddOrUpdate(tx);
                            }
                        }

                        result.AssetTransactions.AddRange(corporateTransactions);
                    }
                    else
                    {
                        (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) =
                            await this._agent.ParseAllAsync(csvFile);

                        foreach (Transaction tx in transactions)
                        {
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                this._transactionRepo.AddOrUpdate(tx);
                            }
                        }

                        foreach (AssetTransaction tx in assetTransactions)
                        {
                            ValidationResult vr = this._validator.Validate(tx);
                            result.ValidationErrors.AddRange(vr.Errors);
                            result.ValidationWarnings.AddRange(vr.Warnings);
                            if (vr.IsValid)
                            {
                                this._portfolioRepo.AddOrUpdate(tx);
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

        this._logger.LogInformation(
            "Import completed: {FilesProcessed} files, {Transactions} transactions, {AssetTransactions} asset transactions, {Errors} errors",
            result.FilesProcessed,
            result.Transactions.Count,
            result.AssetTransactions.Count,
            result.Errors.Count);

        return result;
    }
}

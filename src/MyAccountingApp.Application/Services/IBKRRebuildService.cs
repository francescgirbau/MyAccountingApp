using System.Text;
using System.Text.Json;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <inheritdoc/>
public sealed class IBKRRebuildService : IIBKRRebuildService
{
    private const string IbkrSourcePrefix = "U8997440";

    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;
    private readonly IVaultService _vault;
    private readonly string _backupDirectory;

    public IBKRRebuildService(
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo,
        IVaultService vault,
        string backupDirectory)
    {
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this._portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
        this._optionRepo = optionRepo ?? throw new ArgumentNullException(nameof(optionRepo));
        this._vault = vault ?? throw new ArgumentNullException(nameof(vault));
        this._backupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
    }

    /// <inheritdoc/>
    public IBKRRebuildPreviewDto Preview(IReadOnlyCollection<int> years)
    {
        HashSet<int> targets = new(years);
        RebuildPlan plan = this.BuildPlan(targets);
        return new IBKRRebuildPreviewDto(
            plan.DeletedTransactions.Count,
            plan.DeletedAssets.Count,
            plan.DeletedOptions.Count,
            plan.DeletedOptions.Count(HasNoSource),
            GroupByYear(targets, plan));
    }

    /// <inheritdoc/>
    public IBKRRebuildResultDto Rebuild(IReadOnlyCollection<int> years)
    {
        HashSet<int> targets = new(years);
        RebuildPlan plan = this.BuildPlan(targets);
        string backupFile = this.WriteBackup(
            plan.KeptTransactions.Concat(plan.DeletedTransactions),
            plan.KeptAssets.Concat(plan.DeletedAssets),
            plan.KeptOptions.Concat(plan.DeletedOptions));

        this._transactionRepo.Initialize(plan.KeptTransactions);
        this._portfolioRepo.Initialize(plan.KeptAssets);
        this._optionRepo.Initialize(plan.KeptOptions);

        return new IBKRRebuildResultDto(
            backupFile,
            plan.DeletedTransactions.Count,
            plan.DeletedAssets.Count,
            plan.DeletedOptions.Count,
            plan.DeletedOptions.Count(HasNoSource),
            GroupByYear(targets, plan));
    }

    private static bool IsIbkrSource(string? source)
    {
        return source?.StartsWith(IbkrSourcePrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasNoSource(OptionTransaction option)
    {
        return string.IsNullOrWhiteSpace(option.Transaction.Source);
    }

    private static List<IBKRRebuildYearDto> GroupByYear(HashSet<int> targets, RebuildPlan plan)
    {
        return targets
            .OrderBy(year => year)
            .Select(year => new IBKRRebuildYearDto(
                year,
                plan.DeletedTransactions.Count(t => t.Date.Year == year),
                plan.DeletedAssets.Count(a => a.Transaction.Date.Year == year),
                plan.DeletedOptions.Count(o => o.Transaction.Date.Year == year),
                plan.DeletedOptions.Count(o => o.Transaction.Date.Year == year && HasNoSource(o))))
            .ToList();
    }

    private RebuildPlan BuildPlan(HashSet<int> years)
    {
        RebuildPlan plan = new();

        foreach (Transaction tx in this._transactionRepo.GetAll())
        {
            if (IsIbkrSource(tx.Source) && years.Contains(tx.Date.Year))
            {
                plan.DeletedTransactions.Add(tx);
            }
            else
            {
                plan.KeptTransactions.Add(tx);
            }
        }

        foreach (AssetTransaction asset in this._portfolioRepo.GetAllTransactions())
        {
            if (IsIbkrSource(asset.Source ?? asset.Transaction.Source) && years.Contains(asset.Transaction.Date.Year))
            {
                plan.DeletedAssets.Add(asset);
            }
            else
            {
                plan.KeptAssets.Add(asset);
            }
        }

        foreach (OptionTransaction option in this._optionRepo.GetAll())
        {
            if (years.Contains(option.Transaction.Date.Year)
                && (IsIbkrSource(option.Transaction.Source) || HasNoSource(option)))
            {
                plan.DeletedOptions.Add(option);
            }
            else
            {
                plan.KeptOptions.Add(option);
            }
        }

        return plan;
    }

    private string WriteBackup(
        IEnumerable<Transaction> transactions,
        IEnumerable<AssetTransaction> assets,
        IEnumerable<OptionTransaction> options)
    {
        string json = JsonSerializer.Serialize(
            new { transactions, assetTransactions = assets, optionTransactions = options },
            new JsonSerializerOptions { WriteIndented = true });
        byte[] payload = this._vault.IsUnlocked
            ? this._vault.Encrypt(Encoding.UTF8.GetBytes(json))
            : Encoding.UTF8.GetBytes(json);
        string extension = this._vault.IsUnlocked ? "bin" : "json";

        Directory.CreateDirectory(this._backupDirectory);
        string path = Path.Combine(this._backupDirectory, $"ibkr-rebuild-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
        File.WriteAllBytes(path, payload);
        return path;
    }

    private sealed class RebuildPlan
    {
        public List<Transaction> KeptTransactions { get; } = new();

        public List<Transaction> DeletedTransactions { get; } = new();

        public List<AssetTransaction> KeptAssets { get; } = new();

        public List<AssetTransaction> DeletedAssets { get; } = new();

        public List<OptionTransaction> KeptOptions { get; } = new();

        public List<OptionTransaction> DeletedOptions { get; } = new();
    }
}
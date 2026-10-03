namespace MyAccountingApp.Application.Services;

using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

/// <inheritdoc/>
public sealed class IBKRDataHealthService : IIBKRDataHealthService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="IBKRDataHealthService"/> class.
    /// </summary>
    /// <param name="transactionRepo">Repository holding the cash transactions.</param>
    /// <param name="portfolioRepo">Repository holding the asset transactions.</param>
    /// <param name="optionRepo">Repository holding the option transactions.</param>
    public IBKRDataHealthService(
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo)
    {
        this._transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this._portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
        this._optionRepo = optionRepo ?? throw new ArgumentNullException(nameof(optionRepo));
    }

    /// <inheritdoc/>
    public IBKRDataHealthDto GetReport()
    {
        List<Transaction> transactions = this._transactionRepo.GetAll().ToList();
        List<AssetTransaction> assets = this._portfolioRepo.GetAllTransactions().ToList();
        List<OptionTransaction> options = this._optionRepo.GetAll().ToList();

        return new IBKRDataHealthDto(
            transactions.Count,
            assets.Count,
            options.Count,
            transactions.Count(t => IsIbkr(t.Source)),
            assets.Count(a => IsIbkr(a.Source) || IsIbkr(a.Transaction.Source)),
            options.Count(o => IsIbkr(o.Transaction.Source)),
            BuildYears(transactions, assets, options),
            BuildSources(transactions, assets, options),
            BuildOpenExpiredContracts(options),
            BuildPhantomAssets(assets));
    }

    /// <summary>
    /// Determines whether the given provenance corresponds to an Interactive Brokers
    /// statement file (all IBKR downloads start with the account id "U8997440").
    /// </summary>
    /// <param name="source">The provenance of a row, or null for manual entries.</param>
    /// <returns>True when the row was imported from an IBKR file.</returns>
    private static bool IsIbkr(string? source)
    {
        return source?.StartsWith("U8997440", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static List<IBKRYearSummaryDto> BuildYears(
        List<Transaction> transactions,
        List<AssetTransaction> assets,
        List<OptionTransaction> options)
    {
        HashSet<int> years = transactions.Select(t => t.Date.Year)
            .Concat(assets.Select(a => a.Transaction.Date.Year))
            .Concat(options.Select(o => o.Transaction.Date.Year))
            .ToHashSet();

        return years
            .OrderBy(y => y)
            .Select(y => new IBKRYearSummaryDto(
                y,
                transactions.Count(t => t.Date.Year == y),
                assets.Count(a => a.Transaction.Date.Year == y),
                options.Count(o => o.Transaction.Date.Year == y),
                transactions.Count(t => t.Date.Year == y && IsIbkr(t.Source)),
                assets.Count(a => a.Transaction.Date.Year == y && (IsIbkr(a.Source) || IsIbkr(a.Transaction.Source))),
                options.Count(o => o.Transaction.Date.Year == y && IsIbkr(o.Transaction.Source))))
            .ToList();
    }

    private static List<IBKRSourcesSummaryDto> BuildSources(
        List<Transaction> transactions,
        List<AssetTransaction> assets,
        List<OptionTransaction> options)
    {
        HashSet<string> sources = new(StringComparer.OrdinalIgnoreCase);
        sources.UnionWith(transactions.Select(t => DisplayName(t.Source)));
        sources.UnionWith(assets.Select(a => DisplayName(a.Source ?? a.Transaction.Source)));
        sources.UnionWith(options.Select(o => DisplayName(o.Transaction.Source)));

        return sources
            .OrderBy(s => s)
            .Select(s => new IBKRSourcesSummaryDto(
                s,
                transactions.Count(t => DisplayName(t.Source) == s),
                assets.Count(a => DisplayName(a.Source ?? a.Transaction.Source) == s),
                options.Count(o => DisplayName(o.Transaction.Source) == s)))
            .ToList();
    }

    private static List<IBKROpenExpiredContractDto> BuildOpenExpiredContracts(List<OptionTransaction> options)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        return options
            .GroupBy(OptionPositionService.ContractKey)
            .Select(group =>
            {
                List<OptionTransaction> ordered = group.OrderBy(l => l.Transaction.Date).ToList();
                OptionTransaction first = ordered[0];
                OptionPositionService.TryParseContract(first.Transaction.Description, out _, out DateOnly expiration, out _, out string side);
                return new
                {
                    Ordered = ordered,
                    First = first,
                    Expiration = expiration,
                    Side = side,
                    Net = ordered.Sum(l => l.Type == AssetTransactionType.Buy ? l.Quantity : -l.Quantity),
                };
            })
            .Where(x => x.Expiration != default && x.Expiration < today && x.Net != 0)
            .Select(x =>
            {
                string direction = x.First.Type == AssetTransactionType.Buy ? "Long" : "Short";
                return new IBKROpenExpiredContractDto(
                    x.First.Transaction.Description,
                    $"{direction} {x.Side}",
                    x.Side,
                    x.Net,
                    x.Expiration,
                    DateOnly.FromDateTime(x.First.Transaction.Date),
                    x.Ordered.Count);
            })
            .OrderByDescending(c => c.Expiration)
            .ToList();
    }

    private static List<IBKRPhantomAssetDto> BuildPhantomAssets(List<AssetTransaction> assets)
    {
        return assets
            .Where(a => Math.Abs(a.Quantity) == 1
                && a.Transaction.Category is not TransactionCategory.INVESTMENT and not TransactionCategory.DIVESTMENT)
            .Select(a => new IBKRPhantomAssetDto(
                a.Transaction.Date,
                a.Symbol,
                a.Quantity,
                a.Transaction.Category.ToString(),
                a.Transaction.Money.Amount,
                a.Transaction.Money.Currency,
                a.Source ?? a.Transaction.Source))
            .OrderBy(p => p.Date)
            .ToList();
    }

    private static string DisplayName(string? source)
    {
        return string.IsNullOrWhiteSpace(source) ? "(no source)" : source;
    }
}
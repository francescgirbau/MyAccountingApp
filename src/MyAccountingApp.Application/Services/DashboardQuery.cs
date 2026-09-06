using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class DashboardQuery : IDashboardQuery
{
    private const string Eur = "EUR";

    private readonly ITransactionRepository _transactionRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;
    private readonly IValidationQuery _validationQuery;

    public DashboardQuery(
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo,
        IValidationQuery validationQuery)
    {
        this._transactionRepo = transactionRepo;
        this._portfolioRepo = portfolioRepo;
        this._optionRepo = optionRepo;
        this._validationQuery = validationQuery;
    }

    public Task<DashboardDto> GetAsync(DateOnly asOf)
    {
        List<Transaction> allTransactions = this._transactionRepo.GetAll().ToList();
        List<AssetTransaction> allAssetTransactions = this._portfolioRepo.GetAllTransactions().ToList();
        List<OptionTransaction> allOptionTransactions = this._optionRepo.GetAll().ToList();

        CashSnapshotDto cash = BuildCashSnapshot(allTransactions, allAssetTransactions, allOptionTransactions, asOf);
        PortfolioSnapshotDto portfolio = BuildPortfolioSnapshot(allAssetTransactions, allOptionTransactions, asOf.Year);
        List<DashboardAlertDto> alerts = BuildAlerts(allTransactions, allAssetTransactions, allOptionTransactions);
        this.AddDataQualityAlert(alerts);

        return Task.FromResult(new DashboardDto(asOf, cash, portfolio, alerts));
    }

    private static CashSnapshotDto BuildCashSnapshot(List<Transaction> allTransactions, List<AssetTransaction> allAssetTransactions, List<OptionTransaction> allOptionTransactions, DateOnly asOf)
    {
        DateTime end = asOf.ToDateTime(new TimeOnly(23, 59, 59));
        DateTime yearStart = new DateTime(asOf.Year, 1, 1);
        DateTime monthStart = new DateTime(asOf.Year, asOf.Month, 1);

        decimal SumCategory(List<Transaction> txs, Func<Transaction, bool> predicate) =>
            txs.Where(t => string.Equals(t.Money.Currency, Eur, StringComparison.OrdinalIgnoreCase) && predicate(t)).Sum(t => t.Money.Amount);

        List<Transaction> ytd = allTransactions.Where(t => t.Date >= yearStart && t.Date <= end).ToList();
        List<Transaction> mtd = allTransactions.Where(t => t.Date >= monthStart && t.Date <= end).ToList();

        // Operating breakdown YTD
        decimal incomeYtd = SumCategory(ytd, t => t.Category == TransactionCategory.INCOME);
        decimal dividendsYtd = SumCategory(ytd, t => t.Category == TransactionCategory.DIVIDEND);
        decimal interestYtd = SumCategory(ytd, t => t.Category == TransactionCategory.INTEREST);
        decimal expensesYtd = SumCategory(ytd, t => t.Category == TransactionCategory.EXPENSE);
        decimal feesYtd = SumCategory(ytd, t => t.Category == TransactionCategory.FEE);
        decimal withholdingYtd = SumCategory(ytd, t => t.Category == TransactionCategory.WITHHOLDING_TAX);

        decimal incomeYtdTotal = incomeYtd + SumCategory(ytd, t => t.Category == TransactionCategory.DIVIDEND) + SumCategory(ytd, t => t.Category == TransactionCategory.INTEREST);
        decimal expensesYtdTotal = SumCategory(ytd, t => t.Category == TransactionCategory.EXPENSE) + SumCategory(ytd, t => t.Category == TransactionCategory.FEE) + SumCategory(ytd, t => t.Category == TransactionCategory.WITHHOLDING_TAX);

        // Operating breakdown MTD
        decimal incomeMtd = SumCategory(mtd, t => t.Category == TransactionCategory.INCOME);
        decimal dividendsMtd = SumCategory(mtd, t => t.Category == TransactionCategory.DIVIDEND);
        decimal interestMtd = SumCategory(mtd, t => t.Category == TransactionCategory.INTEREST);
        decimal expenseMtd = SumCategory(mtd, t => t.Category == TransactionCategory.EXPENSE);
        decimal feesMtd = SumCategory(mtd, t => t.Category == TransactionCategory.FEE);
        decimal withholdingMtd = SumCategory(mtd, t => t.Category == TransactionCategory.WITHHOLDING_TAX);

        decimal incomeMtdTotal = incomeMtd + SumCategory(mtd, t => t.Category == TransactionCategory.DIVIDEND) + SumCategory(mtd, t => t.Category == TransactionCategory.INTEREST);
        decimal expensesMtdTotal = SumCategory(mtd, t => t.Category == TransactionCategory.EXPENSE) + SumCategory(mtd, t => t.Category == TransactionCategory.FEE) + SumCategory(mtd, t => t.Category == TransactionCategory.WITHHOLDING_TAX);

        // Internal YTD
        decimal transfersYtd = SumCategory(ytd, t => t.Category == TransactionCategory.TRANSFER);
        decimal depositsYtd = SumCategory(ytd, t => t.Category == TransactionCategory.DEPOSIT);
        decimal fxOutYtd = SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out);
        decimal fxInYtd = SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In);

        // Investing YTD (from AssetTransactions)
        List<AssetTransaction> ytdAssets = allAssetTransactions
            .Where(a => a.Transaction.Date >= yearStart && a.Transaction.Date <= end)
            .ToList();
        List<OptionTransaction> ytdOptions = allOptionTransactions
            .Where(o => o.Transaction.Date >= yearStart && o.Transaction.Date <= end)
            .ToList();
        decimal purchasesYtd = ytdAssets.Where(a => a.Type == AssetTransactionType.Buy).Sum(a => a.Transaction.Money.Amount)
            + ytdOptions.Where(o => o.Type == AssetTransactionType.Buy).Sum(o => o.Transaction.Money.Amount);
        decimal salesYtd = ytdAssets.Where(a => a.Type == AssetTransactionType.Sell).Sum(a => a.Transaction.Money.Amount)
            + ytdOptions.Where(o => o.Type == AssetTransactionType.Sell).Sum(o => o.Transaction.Money.Amount);

        return new CashSnapshotDto(
            new OperatingCashFlowDto(
                Math.Round(incomeMtd + dividendsMtd + interestMtd, 2),
                Math.Round(expenseMtd + feesMtd + withholdingMtd, 2),
                Math.Round(incomeMtd + dividendsMtd + interestMtd - expenseMtd - feesMtd - withholdingMtd, 2)),
            new OperatingCashFlowDto(
                Math.Round(incomeYtd + SumCategory(ytd, t => t.Category == TransactionCategory.DIVIDEND) + SumCategory(ytd, t => t.Category == TransactionCategory.INTEREST), 2),
                Math.Round(expensesYtdTotal, 2),
                Math.Round(incomeYtd + SumCategory(ytd, t => t.Category == TransactionCategory.DIVIDEND) + SumCategory(ytd, t => t.Category == TransactionCategory.INTEREST) - expensesYtdTotal, 2)),
            new InvestingCashFlowDto(
                Math.Round(purchasesYtd, 2),
                Math.Round(salesYtd, 2),
                Math.Round(salesYtd - purchasesYtd, 2)),
            new InternalCashFlowDto(
                Math.Round(transfersYtd, 2),
                Math.Round(depositsYtd, 2),
                Math.Round(SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out), 2),
                Math.Round(SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In), 2),
                Math.Round(SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In) - SumCategory(ytd, t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out), 2)));
    }

    private static PortfolioSnapshotDto BuildPortfolioSnapshot(List<AssetTransaction> allAssetTransactions, List<OptionTransaction> allOptionTransactions, int year)
    {
        decimal totalCostBasis = 0;
        decimal realizedYtd = 0;
        int openPositionCount = 0;
        int symbolCount = 0;

        List<IGrouping<string, AssetTransaction>> stockGroups = allAssetTransactions.GroupBy(t => t.Symbol).ToList();
        List<IGrouping<string, OptionTransaction>> optionGroups = allOptionTransactions.GroupBy(o => o.Symbol).ToList();
        List<string> symbols = stockGroups.Select(g => g.Key).Union(optionGroups.Select(g => g.Key)).ToList();

        foreach (string symbol in symbols)
        {
            symbolCount++;

            IGrouping<string, AssetTransaction>? stockGroup = stockGroups.FirstOrDefault(g => g.Key == symbol);
            IGrouping<string, OptionTransaction>? optionGroup = optionGroups.FirstOrDefault(g => g.Key == symbol);

            FifoPosition? stockPosition = stockGroup is not null ? FifoCalculator.Compute(stockGroup) : null;
            FifoPosition? optionPosition = optionGroup is not null ? FifoCalculator.ComputeOptions(optionGroup) : null;
            FifoPosition position = stockPosition is not null && optionPosition is not null
                ? FifoCalculator.Merge(stockPosition, optionPosition)
                : stockPosition ?? optionPosition!;

            totalCostBasis += position.TotalCostBasis;
            realizedYtd += position.Sales.Where(s => s.Date.Year == year).Sum(s => s.RealizedGainLoss);

            bool isOpen = optionPosition is not null
                ? position.NetQuantity != 0
                : position.NetQuantity > 0;
            if (isOpen)
            {
                openPositionCount++;
            }
        }

        return new PortfolioSnapshotDto(
            Math.Round(totalCostBasis, 2),
            null,
            null,
            Math.Round(realizedYtd, 2),
            openPositionCount,
            symbolCount,
            0);
    }

    private static List<DashboardAlertDto> BuildAlerts(List<Transaction> allTransactions, List<AssetTransaction> allAssetTransactions, List<OptionTransaction> allOptionTransactions)
    {
        List<DashboardAlertDto> alerts = new();

        if (allTransactions.Any(t => t.Money.Currency != Eur)
            || allAssetTransactions.Any(t => t.Transaction.Money.Currency != Eur)
            || allOptionTransactions.Any(o => o.Transaction.Money.Currency != Eur))
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "UNCONVERTED_CURRENCY",
                "Some movements are not in EUR and are excluded from the totals above.",
                "/conversions"));
        }

        return alerts;
    }

    private void AddDataQualityAlert(List<DashboardAlertDto> alerts)
    {
        ValidationResult validation = this._validationQuery.ValidateAll();

        if (validation.Errors.Count > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "error",
                "DATA_QUALITY",
                $"{validation.Errors.Count} data quality error(s) found",
                "/data-quality"));
        }
        else if (validation.Warnings.Count > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "DATA_QUALITY",
                $"{validation.Warnings.Count} data quality warning(s) found",
                "/data-quality"));
        }
    }
}
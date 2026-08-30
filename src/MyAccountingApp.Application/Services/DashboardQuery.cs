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
    private readonly IValidationQuery _validationQuery;

    public DashboardQuery(
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo,
        IValidationQuery validationQuery)
    {
        this._transactionRepo = transactionRepo;
        this._portfolioRepo = portfolioRepo;
        this._validationQuery = validationQuery;
    }

    public Task<DashboardDto> GetAsync(DateOnly asOf)
    {
        List<Transaction> allTransactions = this._transactionRepo.GetAll().ToList();
        List<AssetTransaction> allAssetTransactions = this._portfolioRepo.GetAllTransactions().ToList();

        CashSnapshotDto cash = BuildCashSnapshot(allTransactions, allAssetTransactions, asOf);
        PortfolioSnapshotDto portfolio = BuildPortfolioSnapshot(allAssetTransactions, asOf.Year);
        List<DashboardAlertDto> alerts = BuildAlerts(allTransactions, allAssetTransactions);
        this.AddDataQualityAlert(alerts);

        return Task.FromResult(new DashboardDto(asOf, cash, portfolio, alerts));
    }

    private static CashSnapshotDto BuildCashSnapshot(List<Transaction> allTransactions, List<AssetTransaction> allAssetTransactions, DateOnly asOf)
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
        decimal purchasesYtd = ytdAssets.Where(a => a.Type == AssetTransactionType.Buy).Sum(a => a.Transaction.Money.Amount);
        decimal salesYtd = ytdAssets.Where(a => a.Type == AssetTransactionType.Sell).Sum(a => a.Transaction.Money.Amount);

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

    private static PortfolioSnapshotDto BuildPortfolioSnapshot(List<AssetTransaction> allAssetTransactions, int year)
    {
        decimal totalCostBasis = 0;
        decimal realizedYtd = 0;
        int openPositionCount = 0;
        int symbolCount = 0;

        foreach (IGrouping<string, AssetTransaction> symbolGroup in allAssetTransactions.GroupBy(t => t.Symbol))
        {
            symbolCount++;

            List<AssetTransaction> ordered = symbolGroup.OrderBy(t => t.Transaction.Date).ToList();
            (decimal costBasis, decimal realized) = ComputeFifo(ordered, year);
            totalCostBasis += costBasis;
            realizedYtd += realized;

            if (ordered.Sum(t => t.Type == AssetTransactionType.Buy ? t.Quantity : -t.Quantity) > 0)
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

    private static (decimal CostBasis, decimal Realized) ComputeFifo(List<AssetTransaction> ordered, int year)
    {
        List<FifoLot> lots = new();
        decimal realized = 0;

        foreach (AssetTransaction tx in ordered)
        {
            if (tx.Type == AssetTransactionType.Buy)
            {
                lots.Add(new FifoLot(tx.Quantity, tx.Transaction.Money.Amount));
            }
            else
            {
                decimal sellQty = tx.Quantity;
                decimal matchedCostBasis = 0;

                foreach (FifoLot lot in lots.Where(l => l.RemainingQuantity > 0))
                {
                    if (sellQty <= 0)
                    {
                        break;
                    }

                    decimal consumed = Math.Min(sellQty, lot.RemainingQuantity);
                    matchedCostBasis += consumed * lot.UnitaryCost;
                    decimal proceeds = (consumed / tx.Quantity) * tx.Transaction.Money.Amount;
                    realized += tx.Transaction.Date.Year == year
                        ? (proceeds - (consumed * lot.UnitaryCost))
                        : 0;
                    lot.RemainingQuantity -= consumed;
                    sellQty -= consumed;
                }
            }
        }

        decimal costBasis = lots.Where(l => l.RemainingQuantity > 0).Sum(l => l.RemainingQuantity * l.UnitaryCost);
        return (costBasis, realized);
    }

    private static List<DashboardAlertDto> BuildAlerts(List<Transaction> allTransactions, List<AssetTransaction> allAssetTransactions)
    {
        List<DashboardAlertDto> alerts = new();

        if (allTransactions.Any(t => t.Money.Currency != Eur) || allAssetTransactions.Any(t => t.Transaction.Money.Currency != Eur))
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

    private sealed class FifoLot
    {
        public decimal TotalQuantity { get; }
        public decimal TotalCost { get; }
        public decimal UnitaryCost => this.TotalCost / this.TotalQuantity;
        public decimal RemainingQuantity { get; set; }

        public FifoLot(decimal quantity, decimal totalCost)
        {
            this.TotalQuantity = quantity;
            this.TotalCost = totalCost;
            this.RemainingQuantity = quantity;
        }
    }
}
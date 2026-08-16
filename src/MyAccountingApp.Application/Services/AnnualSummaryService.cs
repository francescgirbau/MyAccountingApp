using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class AnnualSummaryService : IAnnualSummaryService
{
    private readonly ITransactionRepository transactionRepo;
    private readonly IPortfolioRepository portfolioRepo;

    public AnnualSummaryService(
        ITransactionRepository transactionRepo,
        IPortfolioRepository portfolioRepo)
    {
        this.transactionRepo = transactionRepo ?? throw new ArgumentNullException(nameof(transactionRepo));
        this.portfolioRepo = portfolioRepo ?? throw new ArgumentNullException(nameof(portfolioRepo));
    }

    public List<AnnualSummaryDto> GetAll()
    {
        List<Domain.Entities.Transaction> transactions = this.transactionRepo.GetAll().ToList();
        List<Domain.Entities.AssetTransaction> assetTransactions = this.portfolioRepo.GetAllTransactions().ToList();

        var years = transactions
            .Select(t => t.Date.Year)
            .Union(assetTransactions.Select(a => a.Transaction.Date.Year))
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        List<AnnualSummaryDto> summaries = new List<AnnualSummaryDto>(years.Count);

        foreach (int year in years)
        {
            summaries.Add(this.BuildSummary(year, transactions, assetTransactions));
        }

        return summaries;
    }

    public AnnualSummaryDto? GetByYear(int year)
    {
        List<Domain.Entities.Transaction> transactions = this.transactionRepo.GetAll().ToList();
        List<Domain.Entities.AssetTransaction> assetTransactions = this.portfolioRepo.GetAllTransactions().ToList();

        bool hasData = transactions.Any(t => t.Date.Year == year) ||
                       assetTransactions.Any(a => a.Transaction.Date.Year == year);

        if (!hasData)
        {
            return null;
        }

        return this.BuildSummary(year, transactions, assetTransactions);
    }

    private AnnualSummaryDto BuildSummary(
        int year,
        List<Domain.Entities.Transaction> transactions,
        List<Domain.Entities.AssetTransaction> assetTransactions)
    {
        List<Domain.Entities.Transaction> yearTxs = transactions
            .Where(t => t.Date.Year == year)
            .ToList();

        List<Domain.Entities.Transaction> yearEurCashTxs = yearTxs
            .Where(t => string.Equals(t.Money.Currency, "EUR", StringComparison.OrdinalIgnoreCase))
            .ToList();

        List<Domain.Entities.AssetTransaction> yearAssetTxs = assetTransactions
            .Where(a => a.Transaction.Date.Year == year)
            .ToList();

        decimal expenses = yearEurCashTxs
            .Where(t => t.Category.IsCashExpense())
            .Sum(t => t.Money.Amount);

        decimal income = yearEurCashTxs
            .Where(t => t.Category.IsCashIncome())
            .Sum(t => t.Money.Amount);

        decimal transfers = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.TRANSFER || (t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out))
            .Sum(t => t.Money.Amount);

        decimal deposits = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.DEPOSIT || (t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In))
            .Sum(t => t.Money.Amount);

        decimal investmentPurchases = yearAssetTxs
            .Where(a => a.Type == AssetTransactionType.Buy)
            .Sum(a => a.Transaction.Money.Amount);

        decimal investmentSales = yearAssetTxs
            .Where(a => a.Type == AssetTransactionType.Sell)
            .Sum(a => a.Transaction.Money.Amount);

            decimal netCashFlow = income - expenses;

        List<MonthlySummaryDto> months = this.BuildMonthlySummaries(year, yearTxs, yearAssetTxs);

        return new AnnualSummaryDto(
            year,
            Math.Round(expenses, 2),
            Math.Round(income, 2),
            Math.Round(investmentPurchases, 2),
            Math.Round(investmentSales, 2),
            Math.Round(netCashFlow, 2),
            yearTxs.Count,
            yearAssetTxs.Count,
            months,
            Math.Round(transfers, 2),
            Math.Round(deposits, 2),
            IncludesAssetCashFlows: false);
    }

    private List<MonthlySummaryDto> BuildMonthlySummaries(
        int year,
        List<Domain.Entities.Transaction> yearTxs,
        List<Domain.Entities.AssetTransaction> yearAssetTxs)
    {
        List<MonthlySummaryDto> result = new List<MonthlySummaryDto>(12);

        for (int month = 1; month <= 12; month++)
        {
            List<Domain.Entities.Transaction> monthTxs = yearTxs
                .Where(t => t.Date.Month == month)
                .ToList();

            List<Domain.Entities.Transaction> monthEurCashTxs = monthTxs
                .Where(t => string.Equals(t.Money.Currency, "EUR", StringComparison.OrdinalIgnoreCase))
                .ToList();

            List<Domain.Entities.AssetTransaction> monthAssetTxs = yearAssetTxs
                .Where(a => a.Transaction.Date.Month == month)
                .ToList();

            if (monthTxs.Count == 0 && monthAssetTxs.Count == 0)
            {
                continue;
            }

            decimal expenses = monthEurCashTxs
                .Where(t => t.Category.IsCashExpense())
                .Sum(t => t.Money.Amount);

            decimal income = monthEurCashTxs
                .Where(t => t.Category.IsCashIncome())
                .Sum(t => t.Money.Amount);

            decimal transfers = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.TRANSFER || (t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out))
                .Sum(t => t.Money.Amount);

            decimal deposits = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.DEPOSIT || (t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In))
                .Sum(t => t.Money.Amount);

            decimal investmentPurchases = monthAssetTxs
                .Where(a => a.Type == AssetTransactionType.Buy)
                .Sum(a => a.Transaction.Money.Amount);

            decimal investmentSales = monthAssetTxs
                .Where(a => a.Type == AssetTransactionType.Sell)
                .Sum(a => a.Transaction.Money.Amount);

        decimal netCashFlow = income - expenses;

            result.Add(new MonthlySummaryDto(
                month,
                Math.Round(expenses, 2),
                Math.Round(income, 2),
                Math.Round(investmentPurchases, 2),
                Math.Round(investmentSales, 2),
                Math.Round(netCashFlow, 2),
                monthTxs.Count,
                monthAssetTxs.Count,
                Math.Round(transfers, 2),
                Math.Round(deposits, 2)));
        }

        return result;
    }
}

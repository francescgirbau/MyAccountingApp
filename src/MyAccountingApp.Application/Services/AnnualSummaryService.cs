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

    private static (int PairCount, int UnmatchedLegCount) CountFx(List<Domain.Entities.Transaction> yearTxs)
    {
        List<Domain.Entities.Transaction> fxLegs = yearTxs
            .Where(t => t.Category == TransactionCategory.FX_CONVERSION)
            .ToList();

        int pairCount = fxLegs
            .Select(t => t.FxPairId)
            .Where(id => id is not null)
            .Distinct()
            .Count();

        int unmatchedLegCount = fxLegs.Count(t =>
            t.FxPairId is null ||
            !fxLegs.Any(other => other.FxPairId == t.FxPairId && other.Id != t.Id));

        return (pairCount, unmatchedLegCount);
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

        // Operating breakdown
        decimal income = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.INCOME)
            .Sum(t => t.Money.Amount);

        decimal dividends = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.DIVIDEND)
            .Sum(t => t.Money.Amount);

        decimal interestIncome = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.INTEREST)
            .Sum(t => t.Money.Amount);

        decimal expenses = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.EXPENSE)
            .Sum(t => t.Money.Amount);

        decimal fees = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.FEE)
            .Sum(t => t.Money.Amount);

        decimal withholdingTax = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.WITHHOLDING_TAX)
            .Sum(t => t.Money.Amount);

        decimal operatingIncome = income + dividends + interestIncome;
        decimal operatingExpenses = expenses + fees + withholdingTax;
        decimal netOperatingCashFlow = operatingIncome - operatingExpenses;

        // Investing breakdown (from AssetTransactions)
        decimal investmentPurchases = yearAssetTxs
            .Where(a => a.Type == AssetTransactionType.Buy)
            .Sum(a => a.Transaction.Money.Amount);

        decimal investmentSales = yearAssetTxs
            .Where(a => a.Type == AssetTransactionType.Sell)
            .Sum(a => a.Transaction.Money.Amount);

        decimal netInvestedCash = investmentSales - investmentPurchases;

        // Internal breakdown
        decimal transfers = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.TRANSFER)
            .Sum(t => t.Money.Amount);

        decimal deposits = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.DEPOSIT)
            .Sum(t => t.Money.Amount);

        decimal fxOut = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out)
            .Sum(t => t.Money.Amount);

        decimal fxIn = yearEurCashTxs
            .Where(t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In)
            .Sum(t => t.Money.Amount);

        decimal fxNet = fxIn - fxOut;

        (int pairCount, int unmatchedLegCount) = CountFx(yearTxs);

        List<MonthlySummaryDto> months = this.BuildMonthlySummaries(year, yearTxs, yearAssetTxs);

        return new AnnualSummaryDto(
            year,
            new AnnualOperatingDto(
                Math.Round(income, 2),
                Math.Round(expenses, 2),
                Math.Round(dividends, 2),
                Math.Round(interestIncome, 2),
                Math.Round(fees, 2),
                Math.Round(withholdingTax, 2),
                Math.Round(netOperatingCashFlow, 2)),
            new AnnualInvestingDto(
                Math.Round(investmentPurchases, 2),
                Math.Round(investmentSales, 2),
                Math.Round(netInvestedCash, 2)),
            new AnnualInternalDto(
                Math.Round(transfers, 2),
                Math.Round(deposits, 2),
                Math.Round(fxOut, 2),
                Math.Round(fxIn, 2),
                Math.Round(fxNet, 2),
                pairCount,
                unmatchedLegCount),
            months,
            IncludesAssetCashFlows: false,
            yearTxs.Count,
            yearAssetTxs.Count);
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

            // Operating breakdown
            decimal income = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.INCOME)
                .Sum(t => t.Money.Amount);

            decimal dividends = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.DIVIDEND)
                .Sum(t => t.Money.Amount);

            decimal interestIncome = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.INTEREST)
                .Sum(t => t.Money.Amount);

            decimal expenses = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.EXPENSE)
                .Sum(t => t.Money.Amount);

            decimal fees = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.FEE)
                .Sum(t => t.Money.Amount);

            decimal withholdingTax = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.WITHHOLDING_TAX)
                .Sum(t => t.Money.Amount);

            decimal operatingIncome = income + dividends + interestIncome;
            decimal operatingExpenses = expenses + fees + withholdingTax;
            decimal netOperatingCashFlow = operatingIncome - operatingExpenses;

            // Investing breakdown
            decimal investmentPurchases = monthAssetTxs
                .Where(a => a.Type == AssetTransactionType.Buy)
                .Sum(a => a.Transaction.Money.Amount);

            decimal investmentSales = monthAssetTxs
                .Where(a => a.Type == AssetTransactionType.Sell)
                .Sum(a => a.Transaction.Money.Amount);

            decimal netInvestedCash = investmentSales - investmentPurchases;

            // Internal breakdown
            decimal transfers = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.TRANSFER)
                .Sum(t => t.Money.Amount);

            decimal deposits = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.DEPOSIT)
                .Sum(t => t.Money.Amount);

            decimal fxOut = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.Out)
                .Sum(t => t.Money.Amount);

            decimal fxIn = monthEurCashTxs
                .Where(t => t.Category == TransactionCategory.FX_CONVERSION && t.FxLeg == FxLeg.In)
                .Sum(t => t.Money.Amount);

            decimal fxNet = fxIn - fxOut;

            result.Add(new MonthlySummaryDto(
                month,
                new MonthlyOperatingDto(
                    Math.Round(income, 2),
                    Math.Round(expenses, 2),
                    Math.Round(dividends, 2),
                    Math.Round(interestIncome, 2),
                    Math.Round(fees, 2),
                    Math.Round(withholdingTax, 2),
                    Math.Round(netOperatingCashFlow, 2)),
                new MonthlyInvestingDto(
                    Math.Round(investmentPurchases, 2),
                    Math.Round(investmentSales, 2),
                    Math.Round(netInvestedCash, 2)),
                new MonthlyInternalDto(
                    Math.Round(transfers, 2),
                    Math.Round(deposits, 2),
                    Math.Round(fxOut, 2),
                    Math.Round(fxIn, 2),
                    Math.Round(fxNet, 2),
                    0,
                    0),
                monthTxs.Count,
                monthAssetTxs.Count));
        }

        return result;
    }
}
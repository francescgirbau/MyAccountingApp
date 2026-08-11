using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class RealizedGainsReportService : IRealizedGainsReportService
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITransactionRepository _transactionRepo;

    public RealizedGainsReportService(IPortfolioRepository portfolioRepo, ITransactionRepository transactionRepo)
    {
        this._portfolioRepo = portfolioRepo;
        this._transactionRepo = transactionRepo;
    }

    public Task<RealizedGainsReportDto> GetRealizedGainsAsync(int year)
    {
        List<Domain.Entities.AssetTransaction> all = this._portfolioRepo.GetAllTransactions().ToList();

        List<SymbolRealizedGainsDto> symbols = all
            .GroupBy(t => t.Symbol)
            .Select(group =>
            {
                FifoPosition position = FifoCalculator.Compute(group);
                List<RealizedSaleDto> yearSales = position.Sales
                    .Where(s => s.Date.Year == year)
                    .Select(s => new RealizedSaleDto(
                        s.Date,
                        Math.Round(s.Quantity, 4),
                        Math.Round(s.Proceeds, 2),
                        Math.Round(s.CostBasis, 2),
                        Math.Round(s.RealizedGainLoss, 2)))
                    .ToList();

                if (yearSales.Count == 0)
                {
                    return null;
                }

                string currency = group.First().Transaction.Money.Currency;

                return new SymbolRealizedGainsDto(
                    group.Key,
                    currency,
                    Math.Round(yearSales.Sum(s => s.Quantity), 4),
                    Math.Round(yearSales.Sum(s => s.Proceeds), 2),
                    Math.Round(yearSales.Sum(s => s.CostBasis), 2),
                    Math.Round(yearSales.Sum(s => s.RealizedGainLoss), 2),
                    yearSales);
            })
            .Where(s => s is not null)
            .Cast<SymbolRealizedGainsDto>()
            .OrderBy(s => s.Symbol)
            .ToList();

        return Task.FromResult(new RealizedGainsReportDto(
            year,
            Math.Round(symbols.Sum(s => s.RealizedGainLoss), 2),
            symbols));
    }

    public Task<WithholdingReportDto> GetWithholdingAsync(int year)
    {
        List<WithholdingTotalDto> totals = this._transactionRepo.GetAll()
            .Where(t => t.Category == TransactionCategory.WITHHOLDING_TAX && t.Date.Year == year)
            .GroupBy(t => t.Money.Currency)
            .Select(g => new WithholdingTotalDto(
                g.Key,
                Math.Round(g.Sum(t => t.Money.Amount), 2),
                g.Count()))
            .OrderBy(t => t.Currency)
            .ToList();

        return Task.FromResult(new WithholdingReportDto(year, totals));
    }
}

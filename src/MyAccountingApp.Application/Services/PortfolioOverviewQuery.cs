using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

public class PortfolioOverviewQuery : IPortfolioOverviewQuery
{
    private const int MaxNamedSlices = 8;

    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;
    private readonly IMarketPriceService _marketPriceService;
    private readonly IConversionRepository _conversionRepo;

    public PortfolioOverviewQuery(
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo,
        IMarketPriceService marketPriceService,
        IConversionRepository conversionRepo)
    {
        this._portfolioRepo = portfolioRepo;
        this._optionRepo = optionRepo;
        this._marketPriceService = marketPriceService;
        this._conversionRepo = conversionRepo;
    }

    public async Task<PortfolioOverviewDto> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        List<WorkingRow> working = new();
        DateTimeOffset? latestAsOfUtc = null;
        bool isMarketClosed = false;
        int unpricedCount = 0;

        foreach (IGrouping<string, AssetTransaction> group in this._portfolioRepo.GetAllTransactions().GroupBy(t => t.Symbol))
        {
            FifoPosition position = FifoCalculator.Compute(group);
            if (position.NetQuantity <= 0)
            {
                continue;
            }

            string currency = group.First().Transaction.Money.Currency;
            decimal cost = Math.Round(position.TotalCostBasis, 2);

            CachedQuote? lastQuote = await this._marketPriceService.GetLastQuoteAsync(group.Key);
            Money? freshPrice = await this._marketPriceService.GetCachedPriceAsync(group.Key);

            bool isPriced = lastQuote is not null;
            bool isStale = isPriced && freshPrice is null;
            isMarketClosed |= isStale;
            if (!isPriced)
            {
                unpricedCount++;
            }

            decimal? marketValue = isPriced ? Math.Round(lastQuote!.Price.Amount * position.NetQuantity, 2) : null;
            decimal? unrealizedPnL = marketValue is null ? null : Math.Round(marketValue.Value - cost, 2);
            decimal? unrealizedPnLPct = marketValue is null || cost == 0
                ? null
                : Math.Round((marketValue.Value / cost) - 1, 4);

            working.Add(new WorkingRow
            {
                Symbol = group.Key,
                Quantity = position.NetQuantity,
                Cost = cost,
                Currency = currency,
                MarketValue = marketValue,
                UnrealizedPnL = unrealizedPnL,
                UnrealizedPnLPct = unrealizedPnLPct,
                LastPrice = lastQuote?.Price.Amount,
                PriceAsOfUtc = lastQuote?.AsOfUtc,
                IsPriced = isPriced,
                IsStale = isStale,
            });

            if (lastQuote is not null && (latestAsOfUtc is null || lastQuote.AsOfUtc > latestAsOfUtc))
            {
                latestAsOfUtc = lastQuote.AsOfUtc;
            }
        }

        // Convert own-currency values to EUR using the latest rate at or before the as-of date.
        decimal investedEur = 0;
        decimal marketEur = 0;

        foreach (WorkingRow row in working.Where(r => r.IsPriced))
        {
            row.CostEur = this.ToEur(row.Cost, row.Currency, asOf);
            row.MarketValueEur = row.MarketValue is null ? null : this.ToEur(row.MarketValue.Value, row.Currency, asOf);
            if (row.CostEur is null || row.MarketValueEur is null)
            {
                continue;
            }

            investedEur += row.CostEur.Value;
            marketEur += row.MarketValueEur.Value;
        }

        // Weights are only meaningful for positions that contributed to the totals.
        foreach (WorkingRow row in working.Where(r => r.CostEur is not null && r.MarketValueEur is not null))
        {
            row.PurchaseWeight = investedEur == 0 ? null : row.CostEur!.Value / investedEur;
            row.CurrentWeight = marketEur == 0 ? null : row.MarketValueEur!.Value / marketEur;
            if (row.PurchaseWeight is not null && row.CurrentWeight is not null)
            {
                row.WeightDelta = row.CurrentWeight.Value - row.PurchaseWeight.Value;
            }
        }

        List<PortfolioPositionRowDto> rows = working.Select(row => row.ToDto()).ToList();
        decimal pnlPct = investedEur == 0 ? 0 : (marketEur / investedEur) - 1;

        return new PortfolioOverviewDto(
            Math.Round(marketEur, 2),
            Math.Round(investedEur, 2),
            Math.Round(marketEur - investedEur, 2),
            investedEur == 0 ? null : Math.Round(pnlPct, 4),
            rows.Where(r => r.MarketValueEur is not null && r.PriceAsOfUtc is not null).Max(r => r.PriceAsOfUtc),
            isMarketClosed,
            unpricedCount,
            this._optionRepo.GetAll().Select(o => o.Symbol).Distinct().Count(),
            rows,
            BuildSlices(rows, investedEur, current: false),
            BuildSlices(rows, marketEur, current: true));
    }

    private static IReadOnlyList<AllocationSliceDto> BuildSlices(IReadOnlyList<PortfolioPositionRowDto> rows, decimal totalEur, bool current)
    {
        if (totalEur == 0)
        {
            return Array.Empty<AllocationSliceDto>();
        }

        List<PortfolioPositionRowDto> counted = rows
            .Where(r => r.CostEur is not null && r.MarketValueEur is not null)
            .OrderByDescending(r => current ? r.CurrentWeight!.Value : r.PurchaseWeight!.Value)
            .ToList();

        List<AllocationSliceDto> slices = new();
        decimal other = 0;

        for (int i = 0; i < counted.Count; i++)
        {
            PortfolioPositionRowDto row = counted[i];
            decimal valueEur = current ? row.MarketValueEur!.Value : row.CostEur!.Value;
            if (i < MaxNamedSlices)
            {
                slices.Add(new AllocationSliceDto(row.Symbol, Math.Round(valueEur, 2), valueEur / totalEur));
            }
            else
            {
                other += valueEur;
            }
        }

        if (other > 0)
        {
            slices.Add(new AllocationSliceDto("Other", Math.Round(other, 2), other / totalEur));
        }

        return slices;
    }

    private decimal? ToEur(decimal amount, string currency, DateOnly asOf)
    {
        if (currency == "EUR")
        {
            return Math.Round(amount, 2);
        }

        if (!Enum.TryParse(currency, out Currencies currencyCode))
        {
            return null;
        }

        Conversion? conversion = this._conversionRepo.GetLatestOnOrBefore(asOf.ToDateTime(TimeOnly.MinValue));
        if (conversion is null || !conversion.Quotes.TryGetValue(currencyCode, out decimal rate) || rate <= 0)
        {
            return null;
        }

        return Math.Round(amount / rate, 2);
    }

    private sealed class WorkingRow
    {
        public string Symbol { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal Cost { get; init; }
        public string Currency { get; init; } = string.Empty;
        public decimal? MarketValue { get; init; }
        public decimal? UnrealizedPnL { get; init; }
        public decimal? UnrealizedPnLPct { get; init; }
        public decimal? LastPrice { get; init; }
        public DateTimeOffset? PriceAsOfUtc { get; init; }
        public bool IsPriced { get; init; }
        public bool IsStale { get; init; }
        public decimal? CostEur { get; set; }
        public decimal? MarketValueEur { get; set; }
        public decimal? PurchaseWeight { get; set; }
        public decimal? CurrentWeight { get; set; }
        public decimal? WeightDelta { get; set; }

        public PortfolioPositionRowDto ToDto() => new(
            this.Symbol,
            this.Quantity,
            this.Cost,
            this.Currency,
            this.CostEur,
            this.MarketValue,
            this.MarketValueEur,
            this.UnrealizedPnL,
            this.UnrealizedPnLPct,
            this.PurchaseWeight,
            this.CurrentWeight,
            this.WeightDelta,
            this.LastPrice,
            this.PriceAsOfUtc,
            this.IsPriced,
            this.IsStale);
    }
}

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
        int optionSymbolCount = 0;

        List<IGrouping<string, AssetTransaction>> stockGroups = this._portfolioRepo.GetAllTransactions().GroupBy(t => t.Symbol).ToList();
        List<IGrouping<string, OptionTransaction>> optionGroups = this._optionRepo.GetAll().GroupBy(o => o.Symbol).ToList();
        List<string> symbols = stockGroups.Select(g => g.Key).Union(optionGroups.Select(g => g.Key)).ToList();

        foreach (string symbol in symbols)
        {
            IGrouping<string, AssetTransaction>? stockGroup = stockGroups.FirstOrDefault(g => g.Key == symbol);
            IGrouping<string, OptionTransaction>? optionGroup = optionGroups.FirstOrDefault(g => g.Key == symbol);

            FifoPosition? stockPosition = stockGroup is not null ? FifoCalculator.Compute(stockGroup) : null;
            FifoPosition? optionPosition = optionGroup is not null ? FifoCalculator.ComputeOptions(optionGroup) : null;
            FifoPosition position = stockPosition is not null && optionPosition is not null
                ? FifoCalculator.Merge(stockPosition, optionPosition)
                : stockPosition ?? optionPosition!;

            // Include open positions: long (quantity > 0) and short options (quantity < 0).
            // Purely short stock positions (oversold) remain excluded.
            if (position.NetQuantity == 0 || (stockPosition is not null && optionPosition is null && position.NetQuantity < 0))
            {
                continue;
            }

            string currency = stockGroup is not null
                ? stockGroup.First().Transaction.Money.Currency
                : optionGroup!.First().Transaction.Money.Currency;
            decimal cost = Math.Round(position.TotalCostBasis, 2);

            CachedQuote? lastQuote = await this._marketPriceService.GetLastQuoteAsync(symbol);
            Money? freshPrice = await this._marketPriceService.GetCachedPriceAsync(symbol);

            bool isPriced = lastQuote is not null;
            bool isStale = isPriced && freshPrice is null;
            isMarketClosed |= isStale;
            if (!isPriced)
            {
                unpricedCount++;
            }

            decimal? marketValue = isPriced ? Math.Round(lastQuote!.Price.Amount * position.NetQuantity, 2) : null;
            decimal? unrealizedPnL = marketValue is null ? null : Math.Round(marketValue.Value - cost, 2);
            decimal? unrealizedPnLPct = marketValue is null || cost == 0 || position.NetQuantity <= 0
                ? null
                : Math.Round((marketValue.Value / cost) - 1, 4);

            if (optionGroup is not null)
            {
                optionSymbolCount++;
            }

            working.Add(new WorkingRow
            {
                Symbol = symbol,
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
                AssetClass = optionGroup is not null ? (stockGroup is not null ? "Mixed" : "Option") : "Stock",
            });

            if (lastQuote is not null && (latestAsOfUtc is null || lastQuote.AsOfUtc > latestAsOfUtc))
            {
                latestAsOfUtc = lastQuote.AsOfUtc;
            }
        }

        // Convert own-currency values to EUR using the latest rate at or before the as-of date.
        decimal investedEur = 0;
        decimal marketEur = 0;
        decimal investedEurLong = 0;
        decimal marketEurLong = 0;

        foreach (WorkingRow row in working.Where(r => r.IsPriced))
        {
            row.CostEur = this.ToEur(row.Cost, row.Currency, asOf);
            row.MarketValueEur = row.MarketValue is null ? null : this.ToEur(row.MarketValue.Value, row.Currency, asOf);
            if (row.CostEur is null || row.MarketValueEur is null)
            {
                continue;
            }

            bool isLong = row.Quantity > 0;
            investedEur += row.CostEur.Value;
            marketEur += row.MarketValueEur.Value;
            if (isLong)
            {
                investedEurLong += row.CostEur.Value;
                marketEurLong += row.MarketValueEur.Value;
            }
        }

        // Weights are only meaningful for long positions that contributed to the totals.
        foreach (WorkingRow row in working.Where(r => r.CostEur is not null && r.MarketValueEur is not null && r.Quantity > 0))
        {
            row.PurchaseWeight = investedEurLong == 0 ? null : row.CostEur!.Value / investedEurLong;
            row.CurrentWeight = marketEurLong == 0 ? null : row.MarketValueEur!.Value / marketEurLong;
            if (row.PurchaseWeight is not null && row.CurrentWeight is not null)
            {
                row.WeightDelta = row.CurrentWeight.Value - row.PurchaseWeight.Value;
            }
        }

        List<PortfolioPositionRowDto> rows = working.Select(row => row.ToDto()).ToList();
        decimal pnlPct = investedEurLong == 0 ? 0 : (marketEurLong / investedEurLong) - 1;

        return new PortfolioOverviewDto(
            Math.Round(marketEur, 2),
            Math.Round(investedEur, 2),
            Math.Round(marketEur - investedEur, 2),
            investedEurLong == 0 ? null : Math.Round(pnlPct, 4),
            rows.Where(r => r.MarketValueEur is not null && r.PriceAsOfUtc is not null).Max(r => r.PriceAsOfUtc),
            isMarketClosed,
            unpricedCount,
            optionSymbolCount,
            rows,
            BuildSlices(rows, investedEurLong, current: false),
            BuildSlices(rows, marketEurLong, current: true));
    }

    private static IReadOnlyList<AllocationSliceDto> BuildSlices(IReadOnlyList<PortfolioPositionRowDto> rows, decimal totalEur, bool current)
    {
        if (totalEur == 0)
        {
            return Array.Empty<AllocationSliceDto>();
        }

        List<PortfolioPositionRowDto> counted = rows
            .Where(r => r.CostEur is not null && r.MarketValueEur is not null && r.Quantity > 0)
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
        public string AssetClass { get; init; } = "Stock";
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
            this.IsStale,
            this.AssetClass);
    }
}
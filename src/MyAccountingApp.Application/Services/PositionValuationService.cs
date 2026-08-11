using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

/// <inheritdoc/>
public class PositionValuationService : IPositionValuationService
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IPositionEngine _positionEngine;
    private readonly IToEurConverter _toEurConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PositionValuationService"/> class.
    /// </summary>
    /// <param name="portfolioRepo">Repository holding the asset transactions.</param>
    /// <param name="positionEngine">Engine computing positions and market prices.</param>
    /// <param name="toEurConverter">Converter used for the EUR valuation.</param>
    public PositionValuationService(
        IPortfolioRepository portfolioRepo,
        IPositionEngine positionEngine,
        IToEurConverter toEurConverter)
    {
        this._portfolioRepo = portfolioRepo;
        this._positionEngine = positionEngine;
        this._toEurConverter = toEurConverter;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PositionValuationDto>> GetValuationsAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        string[] symbols = this._portfolioRepo.GetAllTransactions().Select(t => t.Symbol).Distinct().ToArray();
        List<PositionValuationDto> result = new();

        foreach (string symbol in symbols)
        {
            PortfolioPositionDto? position = await this._positionEngine.GetPosition(symbol, includePrice: true);

            if (position is null || position.NetQuantity <= 0)
            {
                continue;
            }

            decimal? valueEur = null;
            decimal? unrealizedEur = null;
            decimal? rate = null;
            DateOnly? rateDate = null;
            bool isStale = false;
            string? provider = null;

            if (position.MarketPrice is not null)
            {
                try
                {
                    Money marketValue = new(position.MarketPrice.Value * position.NetQuantity, position.Currency);
                    EurConversionDto converted = await this._toEurConverter.ToEurAsync(marketValue, asOf, cancellationToken);
                    valueEur = converted.AmountEur;
                    rate = converted.Rate;
                    rateDate = converted.RateDate;
                    isStale = converted.IsStale;
                    provider = converted.Provider;

                    if (position.UnrealizedGainLoss is not null)
                    {
                        Money unrealized = new(position.UnrealizedGainLoss.Value, position.Currency);
                        unrealizedEur = (await this._toEurConverter.ToEurAsync(unrealized, asOf, cancellationToken)).AmountEur;
                    }
                }
                catch (ConversionNotAvailableException)
                {
                }
            }

            result.Add(new PositionValuationDto(
                symbol,
                position.Currency,
                position.NetQuantity,
                position.MarketPrice,
                position.UnrealizedGainLoss,
                valueEur,
                unrealizedEur,
                rate,
                rateDate,
                isStale,
                provider));
        }

        return result;
    }
}

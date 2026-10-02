using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

/// <inheritdoc/>
public class PortfolioReturnService : IPortfolioReturnService
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IToEurConverter _toEurConverter;
    private readonly IPositionValuationService _valuationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioReturnService"/> class.
    /// </summary>
    /// <param name="portfolioRepo">Repository holding the asset transactions.</param>
    /// <param name="transactionRepo">Repository holding the cash transactions (dividends, withholding taxes).</param>
    /// <param name="toEurConverter">Converter used to express every flow in EUR at its own date.</param>
    /// <param name="valuationService">Service providing the current market value of the portfolio.</param>
    public PortfolioReturnService(
        IPortfolioRepository portfolioRepo,
        ITransactionRepository transactionRepo,
        IToEurConverter toEurConverter,
        IPositionValuationService valuationService)
    {
        this._portfolioRepo = portfolioRepo;
        this._transactionRepo = transactionRepo;
        this._toEurConverter = toEurConverter;
        this._valuationService = valuationService;
    }

    /// <inheritdoc/>
    public async Task<PortfolioReturnDto> GetReturnAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        List<EurCashFlow> capitalFlows = new();
        List<EurCashFlow> proceedsFlows = new();
        int excluded = 0;

        foreach (AssetTransaction tx in this._portfolioRepo.GetAllTransactions())
        {
            await this.AddInstrumentFlowAsync(
                tx.Type == AssetTransactionType.Buy ? capitalFlows : proceedsFlows,
                tx.Transaction.Money,
                DateOnly.FromDateTime(tx.Transaction.Date),
                asOf,
                tx.Type,
                cancellationToken,
                () => excluded++);
        }

        foreach (Transaction tx in this._transactionRepo.GetAll())
        {
            DateOnly date = DateOnly.FromDateTime(tx.Date);
            if (date > asOf)
            {
                continue;
            }

            if (tx.Category == TransactionCategory.DIVIDEND)
            {
                await this.ConvertAndAppendAsync(proceedsFlows, tx.Money.Amount, tx.Money.Currency, date, cancellationToken, () => excluded++);
            }
            else if (tx.Category == TransactionCategory.WITHHOLDING_TAX)
            {
                await this.ConvertAndAppendAsync(proceedsFlows, -tx.Money.Amount, tx.Money.Currency, date, cancellationToken, () => excluded++);
            }
        }

        IReadOnlyList<PositionValuationDto> valuations = await this._valuationService.GetValuationsAsync(asOf, cancellationToken);
        decimal terminalValueEur = valuations.Sum(v => v.ValueEur ?? 0);

        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(capitalFlows, proceedsFlows, terminalValueEur, asOf);

        return new PortfolioReturnDto(
            result.TotalReturn is null ? null : decimal.Round(result.TotalReturn.Value, 4),
            result.AnnualizedReturn is null ? null : decimal.Round(result.AnnualizedReturn.Value, 4),
            result.AyiYears is null ? null : decimal.Round(result.AyiYears.Value, 2),
            decimal.Round(capitalFlows.Sum(f => f.AmountEur), 2),
            decimal.Round(proceedsFlows.Sum(f => f.AmountEur), 2),
            decimal.Round(terminalValueEur, 2),
            excluded);
    }

    private async Task AddInstrumentFlowAsync(
        List<EurCashFlow> flows,
        Money money,
        DateOnly date,
        DateOnly asOf,
        AssetTransactionType type,
        CancellationToken cancellationToken,
        Action onExcluded)
    {
        if (type == AssetTransactionType.CorporateAction || date > asOf)
        {
            return;
        }

        await this.ConvertAndAppendAsync(flows, money.Amount, money.Currency, date, cancellationToken, onExcluded);
    }

    private async Task ConvertAndAppendAsync(
        List<EurCashFlow> flows,
        decimal amount,
        string currency,
        DateOnly date,
        CancellationToken cancellationToken,
        Action onExcluded)
    {
        try
        {
            EurConversionDto converted = await this._toEurConverter.ToEurAsync(new Money(amount, currency), date, cancellationToken);
            flows.Add(new EurCashFlow(date, converted.AmountEur));
        }
        catch (ConversionNotAvailableException)
        {
            onExcluded();
        }
    }
}
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

/// <inheritdoc/>
public class ToEurConverter : IToEurConverter
{
    private readonly ICurrencyRateService _rateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToEurConverter"/> class.
    /// </summary>
    /// <param name="rateService">The currency rate service used to resolve quotes.</param>
    public ToEurConverter(ICurrencyRateService rateService)
    {
        this._rateService = rateService;
    }

    /// <inheritdoc/>
    public async Task<EurConversionDto> ToEurAsync(Money money, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (money.Currency == "EUR")
        {
            return new EurConversionDto(money.Amount, 1m, date, false, "base");
        }

        IReadOnlyList<FxQuoteDto> quotes = await this._rateService.GetFxQuotesAsync(date.ToDateTime(TimeOnly.MinValue), cancellationToken);
        FxQuoteDto? quote = quotes.FirstOrDefault(q => string.Equals(q.Quote, money.Currency, StringComparison.OrdinalIgnoreCase));

        if (quote is null)
        {
            throw new ConversionNotAvailableException($"No conversion available for {money.Currency} on {date:yyyy-MM-dd}");
        }

        return new EurConversionDto(
            Math.Round(money.Amount / quote.Rate, 2),
            quote.Rate,
            quote.RateDate,
            quote.IsStale,
            quote.Provider);
    }
}

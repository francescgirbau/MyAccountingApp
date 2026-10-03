namespace MyAccountingApp.Application.Services;

using System.Globalization;
using System.Text.RegularExpressions;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

/// <inheritdoc/>
public sealed class OptionPositionService : IOptionPositionService
{
    /// <summary>
    /// Standard US equity option multiplier: one contract covers 100 underlying shares.
    /// </summary>
    private const decimal OptionMultiplier = 100m;

    private static readonly Regex ContractRegex = new(
        @"^(?<root>.+?)\s+(?<expiry>\d{1,2}[A-Z]{3}\d{2,4})\s+(?<strike>\d+(?:[.,]\d+)?)\s+(?<side>[CP])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IOptionTransactionRepository _optionRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionPositionService"/> class.
    /// </summary>
    /// <param name="optionRepo">Repository holding the option transactions.</param>
    public OptionPositionService(IOptionTransactionRepository optionRepo)
    {
        this._optionRepo = optionRepo;
    }

    /// <inheritdoc/>
    public IReadOnlyList<OptionPositionDto> GetPositions()
    {
        return this._optionRepo.GetAll()
            .GroupBy(t => ContractKey(t))
            .Select(g => BuildPosition(g.Key, g.ToList()))
            .OrderByDescending(p => p.OpenDate)
            .ToList();
    }

    internal static string ContractKey(OptionTransaction leg)
    {
        if (TryParseContract(leg.Transaction.Description, out string root, out DateOnly expiration, out decimal strike, out string side))
        {
            string expiry = expiration.ToString("ddMMMyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            return $"{root} {expiry} {strike.ToString("0.####", CultureInfo.InvariantCulture)} {side[0]}";
        }

        return leg.Symbol;
    }

    private static OptionPositionDto BuildPosition(string key, List<OptionTransaction> legs)
    {
        List<OptionTransaction> ordered = legs.OrderBy(l => l.Transaction.Date).ToList();
        OptionTransaction first = ordered[0];

        bool contractParsed = TryParseContract(first.Transaction.Description, out string root, out DateOnly expiration, out decimal strike, out string side);
        string underlying = contractParsed ? root : first.Symbol;

        string direction = first.Type == AssetTransactionType.Buy ? "Buy" : "Sell";
        string strategy = direction == "Sell" ? "Short" : "Long";
        if (contractParsed)
        {
            strategy += " " + side;
        }

        decimal netQuantity = 0;
        decimal credit = 0;
        decimal debit = 0;
        int totalSellContracts = 0;

        foreach (OptionTransaction leg in ordered)
        {
            netQuantity += leg.Type == AssetTransactionType.Buy ? leg.Quantity : -leg.Quantity;
            if (leg.Type == AssetTransactionType.Sell)
            {
                credit += leg.Transaction.Money.Amount;
                totalSellContracts += (int)leg.Quantity;
            }
            else
            {
                debit += leg.Transaction.Money.Amount;
            }
        }

        bool isClosed = netQuantity == 0;
        DateOnly openDate = DateOnly.FromDateTime(first.Transaction.Date);

        DateOnly referenceDate = isClosed
            ? DateOnly.FromDateTime(ordered[^1].Transaction.Date)
            : DateOnly.FromDateTime(DateTime.Today);
        int daysHeld = Math.Max(0, referenceDate.DayNumber - openDate.DayNumber);

        decimal profitLoss = credit - debit;

        decimal capitalAtRisk;
        if (direction == "Sell")
        {
            // Short: worst case is buying the stock back at the strike; the credit received
            // reduces the capital at risk.
            decimal notional = strike * OptionMultiplier * totalSellContracts;
            capitalAtRisk = Math.Max(0, notional - credit);
        }
        else
        {
            // Long: the premium paid is the maximum loss.
            capitalAtRisk = debit;
        }

        decimal? yield = capitalAtRisk == 0 ? null : Math.Round(profitLoss / capitalAtRisk, 4);
        decimal? annualizedReturn = yield is null || daysHeld == 0 ? null : Math.Round(yield.Value * 365m / daysHeld, 4);

        return new OptionPositionDto(
            key,
            contractParsed ? first.Transaction.Description : first.Symbol,
            underlying,
            strategy,
            contractParsed ? side : "?",
            direction,
            contractParsed ? expiration : null,
            contractParsed ? strike : null,
            netQuantity,
            first.Transaction.Money.Currency,
            openDate,
            isClosed ? DateOnly.FromDateTime(ordered[^1].Transaction.Date) : null,
            isClosed ? "Closed" : "Open",
            Math.Round(credit, 2),
            Math.Round(debit, 2),
            Math.Round(profitLoss, 2),
            Math.Round(capitalAtRisk, 2),
            yield,
            daysHeld,
            annualizedReturn);
    }

    internal static bool TryParseContract(string description, out string root, out DateOnly expiration, out decimal strike, out string side)
    {
        root = string.Empty;
        expiration = default;
        strike = 0;
        side = string.Empty;

        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        Match match = ContractRegex.Match(description.Trim());
        if (!match.Success)
        {
            return false;
        }

        root = match.Groups["root"].Value.Trim();
        if (!TryParseExpiration(match.Groups["expiry"].Value, out expiration))
        {
            return false;
        }

        string strikeRaw = match.Groups["strike"].Value.Replace(',', '.');
        if (!decimal.TryParse(strikeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out strike))
        {
            return false;
        }

        side = match.Groups["side"].Value.ToUpperInvariant() == "C" ? "Call" : "Put";
        return true;
    }

    internal static bool TryParseExpiration(string raw, out DateOnly expiration)
    {
        string upper = raw.ToUpperInvariant();
        if (DateTime.TryParseExact(
                upper,
                new[] { "ddMMMyy", "ddMMMyyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
        {
            expiration = DateOnly.FromDateTime(date);
            return true;
        }

        expiration = default;
        return false;
    }
}
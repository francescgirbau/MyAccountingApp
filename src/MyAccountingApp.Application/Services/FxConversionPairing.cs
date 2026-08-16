using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// A reconstructed FX conversion pair: the leg that spends cash and the leg that receives cash.
/// </summary>
public sealed record FxConversionPair(
    Guid PairId,
    Transaction Out,
    Transaction In,
    decimal ImpliedRate,
    decimal? BrokerRate,
    string? ExternalKey);

/// <summary>
/// Groups the two legs of FX conversions and detects orphan legs.
/// </summary>
public static class FxConversionPairing
{
    /// <summary>
    /// Maximum number of calendar days the two legs of a pair may be apart.
    /// </summary>
    public const int MaxDaysBetweenLegs = 3;

    /// <summary>
    /// Maximum relative difference accepted between the broker rate and the implied rate.
    /// </summary>
    public const decimal RateTolerance = 0.005m;

    /// <summary>
    /// Reconstructs the pairs for all FX conversion legs that hold both sides.
    /// </summary>
    /// <param name="transactions">The transactions to group.</param>
    /// <returns>The valid pairs, ordered by the date of the Out leg.</returns>
    public static IReadOnlyList<FxConversionPair> Group(IEnumerable<Transaction> transactions)
    {
        List<Transaction> all = transactions.ToList();
        List<FxConversionPair> pairs = new();

        foreach (IGrouping<Guid, Transaction> group in all
            .Where(t => t.FxPairId is not null)
            .GroupBy(t => t.FxPairId!.Value))
        {
            List<Transaction> legs = group.ToList();
            Transaction? outLeg = legs.SingleOrDefault(t => t.FxLeg == FxLeg.Out);
            Transaction? inLeg = legs.SingleOrDefault(t => t.FxLeg == FxLeg.In);

            if (outLeg is null || inLeg is null || legs.Count != 2)
            {
                continue;
            }

            if (ValidatePair(outLeg, inLeg).Count == 0)
            {
                pairs.Add(BuildPair(outLeg, inLeg));
            }
        }

        return pairs.OrderBy(p => p.Out.Date).ToList();
    }

    /// <summary>
    /// Returns the legs whose pair does not hold exactly one Out and one In side.
    /// </summary>
    /// <param name="transactions">The transactions to inspect.</param>
    /// <returns>The orphan legs.</returns>
    public static IReadOnlyList<Transaction> UnmatchedFxLegs(IEnumerable<Transaction> transactions)
    {
        List<Transaction> all = transactions.ToList();
        List<Transaction> unmatched = new();

        foreach (IGrouping<Guid, Transaction> group in all
            .Where(t => t.FxPairId is not null)
            .GroupBy(t => t.FxPairId!.Value))
        {
            List<Transaction> legs = group.ToList();
            int outCount = legs.Count(t => t.FxLeg == FxLeg.Out);
            int inCount = legs.Count(t => t.FxLeg == FxLeg.In);

            if (outCount != 1 || inCount != 1)
            {
                unmatched.AddRange(legs);
            }
        }

        return unmatched;
    }

    /// <summary>
    /// Validates the invariants of an FX pair.
    /// </summary>
    /// <param name="outLeg">The leg spending cash.</param>
    /// <param name="inLeg">The leg receiving cash.</param>
    /// <returns>A list of violated invariants; empty when the pair is valid.</returns>
    public static IReadOnlyList<string> ValidatePair(Transaction outLeg, Transaction inLeg)
    {
        List<string> issues = new();

        if (outLeg.FxPairId != inLeg.FxPairId)
        {
            issues.Add("Both legs must share the same FxPairId.");
        }

        if (outLeg.FxLeg != FxLeg.Out)
        {
            issues.Add("The out leg must have FxLeg.Out.");
        }

        if (inLeg.FxLeg != FxLeg.In)
        {
            issues.Add("The in leg must have FxLeg.In.");
        }

        if (outLeg.Category != TransactionCategory.FX_CONVERSION || inLeg.Category != TransactionCategory.FX_CONVERSION)
        {
            issues.Add("Both legs must be FX_CONVERSION.");
        }

        if (outLeg.Money.Currency == inLeg.Money.Currency)
        {
            issues.Add($"Both legs are in the same currency ({outLeg.Money.Currency}).");
        }

        double days = (inLeg.Date.Date - outLeg.Date.Date).TotalDays;
        if (Math.Abs(days) > MaxDaysBetweenLegs)
        {
            issues.Add($"Legs are {Math.Abs(days):0} day(s) apart (max {MaxDaysBetweenLegs}).");
        }

        decimal? brokerRate = outLeg.FxBrokerRate ?? inLeg.FxBrokerRate;
        if (brokerRate is not null)
        {
            decimal implied = inLeg.Money.Amount / outLeg.Money.Amount;
            if (!RateMatches(implied, brokerRate.Value) && !RateMatches(1 / implied, brokerRate.Value))
            {
                issues.Add($"Broker rate {brokerRate.Value:G} does not match the implied rate {implied:G} within {RateTolerance:P2}.");
            }
        }

        return issues;
    }

    /// <summary>
    /// Computes the rate of a fulfilled pair as received amount per spent amount.
    /// </summary>
    public static decimal ImpliedRate(Transaction outLeg, Transaction inLeg) =>
        inLeg.Money.Amount / outLeg.Money.Amount;

    private static FxConversionPair BuildPair(Transaction outLeg, Transaction inLeg) =>
        new(
            outLeg.FxPairId!.Value,
            outLeg,
            inLeg,
            ImpliedRate(outLeg, inLeg),
            outLeg.FxBrokerRate ?? inLeg.FxBrokerRate,
            outLeg.FxExternalKey ?? inLeg.FxExternalKey);

    private static bool RateMatches(decimal implied, decimal brokerRate)
    {
        decimal delta = Math.Abs(implied - brokerRate) / brokerRate;
        return delta <= RateTolerance;
    }
}
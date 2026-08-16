using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class FxConversionPairingTests
{
    private static Transaction CreateLeg(Guid pairId, FxLeg leg, decimal amount, string currency, DateTime date, decimal? rate = null, string? externalKey = null)
    {
        Transaction tx = new(date, $"FX {leg}", new Money(amount, currency), TransactionCategory.FX_CONVERSION);
        tx.SetFxPair(pairId, leg, rate, externalKey);
        return tx;
    }

    [Fact]
    public void IsFxConversion_ReturnsTrue_OnlyForFxConversion()
    {
        Assert.True(TransactionCategory.FX_CONVERSION.IsFxConversion());
        Assert.False(TransactionCategory.INCOME.IsFxConversion());
        Assert.False(TransactionCategory.TRANSFER.IsFxConversion());
    }

    [Fact]
    public void FxConversion_IsNeitherCashIncomeNorCashExpense()
    {
        Assert.False(TransactionCategory.FX_CONVERSION.IsCashIncome());
        Assert.False(TransactionCategory.FX_CONVERSION.IsCashExpense());
    }

    [Fact]
    public void FxConversion_IsInternalCashMove()
    {
        Assert.True(TransactionCategory.FX_CONVERSION.IsInternalCashMove());
        Assert.True(TransactionCategory.TRANSFER.IsInternalCashMove());
        Assert.True(TransactionCategory.DEPOSIT.IsInternalCashMove());
        Assert.False(TransactionCategory.INCOME.IsInternalCashMove());
    }

    [Fact]
    public void Group_ReconstructsCompletePair()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 490.24m, "EUR", new DateTime(2022, 2, 24), rate: 1.1121m, externalKey: "64900984-84e2-4611-923d-958f45aa2d55");
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 545.20m, "USD", new DateTime(2022, 2, 24), rate: 1.1121m, externalKey: "64900984-84e2-4611-923d-958f45aa2d55");

        IReadOnlyList<FxConversionPair> pairs = FxConversionPairing.Group(new[] { outLeg, inLeg });

        FxConversionPair pair = Assert.Single(pairs);
        Assert.Equal(pairId, pair.PairId);
        Assert.Equal(outLeg.Id, pair.Out.Id);
        Assert.Equal(inLeg.Id, pair.In.Id);
        Assert.Equal(545.20m / 490.24m, pair.ImpliedRate);
        Assert.Equal(1.1121m, pair.BrokerRate);
        Assert.Equal("64900984-84e2-4611-923d-958f45aa2d55", pair.ExternalKey);
    }

    [Fact]
    public void Group_IgnoresOrphanLegs()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 916m, "EUR", new DateTime(2023, 2, 2));

        IReadOnlyList<FxConversionPair> pairs = FxConversionPairing.Group(new[] { outLeg });

        Assert.Empty(pairs);
    }

    [Fact]
    public void UnmatchedFxLegs_ReturnsLegsWithoutComplementarySide()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 916m, "EUR", new DateTime(2023, 2, 2));

        IReadOnlyList<Transaction> unmatched = FxConversionPairing.UnmatchedFxLegs(new[] { outLeg });

        Transaction leg = Assert.Single(unmatched);
        Assert.Equal(outLeg.Id, leg.Id);
    }

    [Fact]
    public void UnmatchedFxLegs_IsEmpty_WhenPairComplete()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 916m, "EUR", new DateTime(2023, 2, 2));
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 999.5392m, "USD", new DateTime(2023, 2, 2));

        IReadOnlyList<Transaction> unmatched = FxConversionPairing.UnmatchedFxLegs(new[] { outLeg, inLeg });

        Assert.Empty(unmatched);
    }

    [Fact]
    public void ValidatePair_RejectsSameCurrencyLegs()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 916m, "EUR", new DateTime(2023, 2, 2), rate: 1.0912m);
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 999.5392m, "EUR", new DateTime(2023, 2, 2), rate: 1.0912m);

        IReadOnlyList<string> issues = FxConversionPairing.ValidatePair(outLeg, inLeg);

        Assert.Contains(issues, i => i.Contains("same currency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePair_RejectsRateMismatch_BeyondTolerance()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 100m, "EUR", new DateTime(2023, 2, 2), rate: 1.10m);
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 100m, "USD", new DateTime(2023, 2, 2), rate: 1.10m);

        IReadOnlyList<string> issues = FxConversionPairing.ValidatePair(outLeg, inLeg);

        Assert.Contains(issues, i => i.Contains("Broker rate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Contains("same currency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePair_RejectsLegsMoreThanThreeDaysApart()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 916m, "EUR", new DateTime(2023, 2, 2), rate: 1.0912m);
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 999.5392m, "USD", new DateTime(2023, 2, 10), rate: 1.0912m);

        IReadOnlyList<string> issues = FxConversionPairing.ValidatePair(outLeg, inLeg);

        Assert.Contains(issues, i => i.Contains("day(s) apart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePair_AcceptsInverseRateOrientation()
    {
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = CreateLeg(pairId, FxLeg.Out, 2.63m, "USD", new DateTime(2022, 3, 12), rate: 1.0940m);
        Transaction inLeg = CreateLeg(pairId, FxLeg.In, 2.40m, "EUR", new DateTime(2022, 3, 12), rate: 1.0940m);

        IReadOnlyList<string> issues = FxConversionPairing.ValidatePair(outLeg, inLeg);

        Assert.Empty(issues);
    }

    [Fact]
    public void SetFxPair_Throws_WhenNotFxConversion()
    {
        Transaction tx = new(new DateTime(2023, 1, 1), "Not FX", new Money(10m, "EUR"), TransactionCategory.INCOME);

        Assert.Throws<InvalidOperationException>(() => tx.SetFxPair(Guid.NewGuid(), FxLeg.Out));
    }

    [Fact]
    public void UpdateCategory_Throws_WhenRecategorizingToFxWithoutPair()
    {
        Transaction tx = new(new DateTime(2023, 1, 1), "Plain", new Money(10m, "EUR"), TransactionCategory.INCOME);

        Assert.Throws<ArgumentException>(() => tx.UpdateCategory(TransactionCategory.FX_CONVERSION));
    }

    [Fact]
    public void UpdateCategory_ClearsFxFields_WhenMovingAwayFromFx()
    {
        Guid pairId = Guid.NewGuid();
        Transaction tx = CreateLeg(pairId, FxLeg.Out, 10m, "EUR", new DateTime(2023, 1, 1), rate: 1.1m);

        tx.UpdateCategory(TransactionCategory.TRANSFER);

        Assert.Equal(TransactionCategory.TRANSFER, tx.Category);
        Assert.Null(tx.FxPairId);
        Assert.Null(tx.FxLeg);
        Assert.Null(tx.FxBrokerRate);
    }
}
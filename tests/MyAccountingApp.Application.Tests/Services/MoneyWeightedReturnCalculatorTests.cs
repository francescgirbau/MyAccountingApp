using MyAccountingApp.Application.Services;

namespace MyAccountingApp.Application.Tests.Services;

public class MoneyWeightedReturnCalculatorTests
{
    [Fact]
    public void Compute_Annualizes_WhenAverageHoldingPeriodIsTwoYears()
    {
        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            new[] { new EurCashFlow(new DateOnly(2020, 1, 1), 1000m) },
            Array.Empty<EurCashFlow>(),
            1210m,
            new DateOnly(2022, 1, 1));

        Assert.Equal(0.21m, result.TotalReturn);                       // (1210 - 1000) / 1000
        Assert.Equal(0.10m, result.AnnualizedReturn!.Value, 2);        // ≈ 1.21^(1/2) - 1 (AYI = 731/365.25)
        Assert.Equal(2m, result.AyiYears!.Value, 2);
    }

    [Fact]
    public void Compute_DoesNotAnnualize_WhenAverageHoldingPeriodIsBelowOneYear()
    {
        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            new[] { new EurCashFlow(new DateOnly(2025, 6, 1), 1000m) },
            Array.Empty<EurCashFlow>(),
            1100m,
            new DateOnly(2026, 1, 1));

        Assert.Equal(0.10m, result.TotalReturn);
        Assert.Null(result.AnnualizedReturn);
        Assert.InRange(result.AyiYears!.Value, 0.5m, 0.6m);
    }

    [Fact]
    public void Compute_AddsDividendsToProceeds()
    {
        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            new[] { new EurCashFlow(new DateOnly(2020, 1, 1), 1000m) },
            new[] { new EurCashFlow(new DateOnly(2021, 1, 1), 50m) },
            1100m,
            new DateOnly(2022, 1, 1));

        Assert.Equal(0.15m, result.TotalReturn);                       // (50 + 1100 - 1000) / 1000
    }

    [Fact]
    public void Compute_DeductsWithholdingTaxesFromProceeds()
    {
        EurCashFlow[] proceeds = new[]
        {
            new EurCashFlow(new DateOnly(2021, 1, 1), 50m),
            new EurCashFlow(new DateOnly(2021, 1, 1), -10m),
        };

        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            new[] { new EurCashFlow(new DateOnly(2020, 1, 1), 1000m) },
            proceeds,
            1100m,
            new DateOnly(2022, 1, 1));

        Assert.Equal(0.14m, result.TotalReturn);                       // (40 + 1100 - 1000) / 1000
    }

    [Fact]
    public void Compute_WeightsHoldingPeriod_ByCapitalAmount()
    {
        EurCashFlow[] capital = new[]
        {
            new EurCashFlow(new DateOnly(2020, 1, 1), 100m),
            new EurCashFlow(new DateOnly(2021, 1, 1), 900m),
        };

        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(capital, Array.Empty<EurCashFlow>(), 1000m, new DateOnly(2022, 1, 1));

        Assert.Equal(1.1m, result.AyiYears!.Value, 2);                 // (100*2 + 900*1) / 1000
        Assert.NotNull(result.AnnualizedReturn);
    }

    [Fact]
    public void Compute_ReturnsNulls_WhenNoCapitalFlow()
    {
        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            Array.Empty<EurCashFlow>(),
            new[] { new EurCashFlow(new DateOnly(2021, 1, 1), 100m) },
            150m,
            new DateOnly(2022, 1, 1));

        Assert.Null(result.TotalReturn);
        Assert.Null(result.AnnualizedReturn);
        Assert.Null(result.AyiYears);
    }

    [Fact]
    public void Compute_ReturnsNullTotal_WhenLossExceedsInvestedCapital()
    {
        MoneyWeightedReturnResult result = MoneyWeightedReturnCalculator.Compute(
            new[] { new EurCashFlow(new DateOnly(2020, 1, 1), 100m) },
            Array.Empty<EurCashFlow>(),
            -50m,
            new DateOnly(2022, 1, 1));

        Assert.Null(result.TotalReturn);
        Assert.Null(result.AnnualizedReturn);
        Assert.NotNull(result.AyiYears);
    }
}
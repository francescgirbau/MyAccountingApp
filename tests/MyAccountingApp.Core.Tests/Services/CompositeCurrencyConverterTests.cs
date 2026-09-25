using MyAccountingApp.Core.Http.Currency;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Core.Tests.Services;

public class CompositeCurrencyConverterTests
{
    [Fact]
    public async Task FetchAllRatesAsync_MergesDefaultAndDedicatedProviders()
    {
        // Arrange
        FakeCurrencyConverter defaultProvider = new();
        BtcOnlyCurrencyConverter dedicatedProvider = new();
        CompositeCurrencyConverter composite = new(
            defaultProvider,
            new Dictionary<Currencies, ICurrencyConverter> { [Currencies.BTC] = dedicatedProvider });

        // Act
        Dictionary<string, decimal> result = await composite.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Equal(1.1m, result["EURUSD"]);
        Assert.Equal(1.5m, result["EURCAD"]);
        Assert.Equal(0.00001666m, result["EURBTC"]);
        Assert.Equal(1, dedicatedProvider.FetchAllCalls);
    }

    [Fact]
    public async Task FetchAllRatesAsync_SkipsExcludedCurrency()
    {
        // Arrange
        FakeCurrencyConverter defaultProvider = new();
        BtcOnlyCurrencyConverter dedicatedProvider = new();
        CompositeCurrencyConverter composite = new(
            defaultProvider,
            new Dictionary<Currencies, ICurrencyConverter> { [Currencies.BTC] = dedicatedProvider },
            new[] { "BTC" });

        // Act
        Dictionary<string, decimal> result = await composite.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Equal(1.1m, result["EURUSD"]);
        Assert.False(result.ContainsKey("EURBTC"));
        Assert.Equal(0, dedicatedProvider.FetchAllCalls);
    }

    [Fact]
    public async Task FetchRangeAsync_MergesRangesByDate()
    {
        // Arrange
        FakeCurrencyConverter defaultProvider = new();
        BtcOnlyCurrencyConverter dedicatedProvider = new();
        CompositeCurrencyConverter composite = new(
            defaultProvider,
            new Dictionary<Currencies, ICurrencyConverter> { [Currencies.BTC] = dedicatedProvider });

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await composite.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1.1m, result[new DateOnly(2025, 1, 1)]["EURUSD"]);
        Assert.Equal(0.00001666m, result[new DateOnly(2025, 1, 1)]["EURBTC"]);
        Assert.Equal(1, dedicatedProvider.FetchRangeCalls);
    }

    [Fact]
    public async Task FetchRangeAsync_SkipsDedicatedProvider_WhenTargetsExcludeBtc()
    {
        // Arrange
        FakeCurrencyConverter defaultProvider = new();
        BtcOnlyCurrencyConverter dedicatedProvider = new();
        CompositeCurrencyConverter composite = new(
            defaultProvider,
            new Dictionary<Currencies, ICurrencyConverter> { [Currencies.BTC] = dedicatedProvider });

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await composite.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2), new[] { Currencies.USD });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1.1m, result[new DateOnly(2025, 1, 1)]["EURUSD"]);
        Assert.False(result[new DateOnly(2025, 1, 1)].ContainsKey("EURBTC"));
        Assert.Equal(0, dedicatedProvider.FetchRangeCalls);
    }

    [Fact]
    public async Task FetchRangeAsync_DoesNotCallDefault_WhenAllTargetsAreDedicated()
    {
        // Arrange
        FakeCurrencyConverter defaultProvider = new();
        BtcOnlyCurrencyConverter dedicatedProvider = new();
        CompositeCurrencyConverter composite = new(
            defaultProvider,
            new Dictionary<Currencies, ICurrencyConverter> { [Currencies.BTC] = dedicatedProvider });

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await composite.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2), new[] { Currencies.BTC });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(0.00001666m, result[new DateOnly(2025, 1, 1)]["EURBTC"]);
        Assert.False(result[new DateOnly(2025, 1, 1)].ContainsKey("EURUSD"));
        Assert.Equal(0, defaultProvider.FetchRangeCalls);
        Assert.Equal(1, dedicatedProvider.FetchRangeCalls);
    }

    private sealed class BtcOnlyCurrencyConverter : ICurrencyConverter
    {
        /// <summary>
        /// Gets the number of times a single-day fetch was called.
        /// </summary>
        public int FetchAllCalls { get; private set; }

        /// <summary>
        /// Gets the number of times a range fetch was called.
        /// </summary>
        public int FetchRangeCalls { get; private set; }

        public Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
        {
            this.FetchAllCalls++;
            return Task.FromResult(new Dictionary<string, decimal> { { "EURBTC", 0.00001666m } });
        }

        public Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
            Currencies source,
            DateOnly start,
            DateOnly end,
            IReadOnlyCollection<Currencies>? targets = null,
            CancellationToken cancellationToken = default)
        {
            this.FetchRangeCalls++;
            Dictionary<DateOnly, Dictionary<string, decimal>> result = new();

            for (DateOnly day = start; day <= end; day = day.AddDays(1))
            {
                result[day] = new Dictionary<string, decimal> { { "EURBTC", 0.00001666m } };
            }

            return Task.FromResult<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>>(result);
        }
    }
}
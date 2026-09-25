using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeCurrencyConverter : ICurrencyConverter
{
    private readonly Dictionary<string, decimal> _quotes;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCurrencyConverter"/> class with default quotes.
    /// </summary>
    public FakeCurrencyConverter()
        : this(new Dictionary<string, decimal>
        {
            { "EURUSD", 1.1m },
            { "EURCAD", 1.5m },
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCurrencyConverter"/> class with custom quotes.
    /// </summary>
    /// <param name="quotes">The pair-code to rate map returned by every fetch.</param>
    public FakeCurrencyConverter(Dictionary<string, decimal> quotes)
    {
        this._quotes = quotes;
    }

    /// <summary>
    /// Gets the number of times a single-day fetch was called.
    /// </summary>
    public int FetchAllCalls { get; private set; }

    /// <summary>
    /// Gets the number of times a range fetch was called.
    /// </summary>
    public int FetchRangeCalls { get; private set; }

    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        this.FetchAllCalls++;
        await Task.Delay(1); // simulate async
        return new Dictionary<string, decimal>(this._quotes);
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
            result[day] = new Dictionary<string, decimal>(this._quotes);
        }

        return Task.FromResult<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>>(result);
    }
}

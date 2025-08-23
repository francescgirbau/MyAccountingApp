using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Core.Entities;

public class Conversion
{
    public Guid Id { get; }
    public DateTime Date { get; }
    public Currencies Source { get; }
    public Dictionary<Currencies, double> Quotes { get; }

    public Conversion(DateTime date, Currencies source, Dictionary<Currencies, double>? quotes = null)
    {
        Id = Guid.NewGuid();
        Date = date.Date;
        Source = source;
        Quotes = quotes ?? new Dictionary<Currencies, double>();

        Validate();
    }

    private void Validate()
    {
        if (Source != Currencies.EUR)
            throw new ArgumentException(
                $"The source currency must be EUR. Provided: {Source}",
                nameof(Source));
    }

    public void AddOrUpdateQuote(Currencies target, double rate)
    {
        if (target == Source)
            throw new InvalidOperationException("Target currency cannot be the same as source currency.");

        Quotes[target] = rate;
    }

    public bool TryGetQuote(Currencies target, out double rate)
    {
        return Quotes.TryGetValue(target, out rate);
    }

    public bool MatchesDate(DateTime date)
    {
        return Date.Date == date.Date;
    }

}
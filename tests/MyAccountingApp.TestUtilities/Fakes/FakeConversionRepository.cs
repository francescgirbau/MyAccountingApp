using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeConversionRepository : IConversionRepository
{
    private readonly List<Conversion> _conversions = new();
    public bool CalledAdd { get; private set; } = false;

    private readonly DateTime _fakeDate = new DateTime(2005, 12, 1);

    private readonly Currencies _fakeSource = Currencies.EUR;

    private readonly Dictionary<Currencies, double> _fakeQuotes = new()
        {
            { Currencies.USD, 1.1 },
            { Currencies.CAD, 1.5 },
        };

    public FakeConversionRepository()
    {
        Conversion conversion = new(this._fakeDate, this._fakeSource, this._fakeQuotes);
        this.AddOrUpdate(conversion);
        this.CalledAdd = false; // We want to add only if it is added externally
    }

    public void AddOrUpdate(Conversion conversion)
    {
        this.CalledAdd = true;
        this._conversions.Add(conversion);
    }

    public IEnumerable<Conversion> GetAll()
    {
        return this._conversions;
    }

    public Conversion? GetByDate(DateTime date)
    {
        return this._conversions.FirstOrDefault(c => c.Date.Date == date.Date);
    }

    public void Initialize(IEnumerable<Conversion> conversions)
    {
        this._conversions.Clear();
        this._conversions.AddRange(conversions);
    }
}

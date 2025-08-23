using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyAccountingApp.Tests.Fakes;

public class FakeConversionRepository : IConversionRepository
{
    private readonly List<Conversion> _conversions = new();
    public bool CalledAdd { get; private set; } = false;

    private readonly DateTime _fakeDate= new DateTime(2005, 12, 1);

    private readonly Currencies _fakeSource = Currencies.EUR;

    private readonly Dictionary<Currencies, double> _fakeQuotes = new()
        {
            { Currencies.USD, 1.1 },
            { Currencies.CAD, 1.5 }
        };

    public FakeConversionRepository() {

        Conversion conversion = new(this._fakeDate, this._fakeSource, this._fakeQuotes);
        Add(conversion);
        CalledAdd = false; // We want to add only if it is added externally

    }


    public void Add(Conversion conversion)
    {
        CalledAdd = true;
        _conversions.Add(conversion);
    }

    public bool ExistsForDate(DateTime date)
    {
        return _conversions.Any(c => c.Date.Date == date.Date);
    }

    public IEnumerable<Conversion> GetAll()
    {
        return _conversions;
    }

    public Conversion? GetByDate(DateTime date)
    {
        return _conversions.FirstOrDefault(c => c.Date.Date == date.Date);
    }
}

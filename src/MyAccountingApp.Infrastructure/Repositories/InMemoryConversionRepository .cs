using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

public class InMemoryConversionRepository : IConversionRepository
{
    private readonly List<Conversion> _conversions = new();

    public void Add(Conversion conversion)
    {
        if (!_conversions.Any(c => c.MatchesDate(conversion.Date)))
        {
            _conversions.Add(conversion);
        }
        else
        {
            throw new InvalidOperationException($"Ja existeix una conversió per la data {conversion.Date:yyyy-MM-dd}");
        }
    }

    public bool ExistsForDate(DateTime date)
    {
        return _conversions.Any(c => c.MatchesDate(date));
    }

    public IEnumerable<Conversion> GetAll()
    {
        return _conversions;
    }

    public Conversion? GetByDate(DateTime date)
    {
        return _conversions.FirstOrDefault(c => c.MatchesDate(date));
    }
}

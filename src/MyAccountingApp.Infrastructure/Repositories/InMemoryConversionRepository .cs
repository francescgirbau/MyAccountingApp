using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// In-memory repository for storing and retrieving currency conversions.
/// Intended for fast, non-persistent operations such as testing or caching.
/// </summary>
public class InMemoryConversionRepository : IConversionRepository
{
    private readonly List<Conversion> _conversions = new();

    /// <summary>
    /// Adds a new conversion to the repository if one for the same date does not already exist.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if a conversion for the date already exists.</exception>
    public void Add(Conversion conversion)
    {
        if (!this._conversions.Any(c => c.MatchesDate(conversion.Date)))
        {
            this._conversions.Add(conversion);
        }
        else
        {
            throw new InvalidOperationException($"A conversion already exists for the date {conversion.Date:yyyy-MM-dd}.");
        }
    }

    /// <summary>
    /// Determines whether a conversion exists for the specified date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if a conversion exists; otherwise, false.</returns>
    public bool ExistsForDate(DateTime date)
    {
        return this._conversions.Any(c => c.MatchesDate(date));
    }

    /// <summary>
    /// Gets all conversions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    public IEnumerable<Conversion> GetAll()
    {
        return this._conversions;
    }

    /// <summary>
    /// Gets the conversion for the specified date, or null if not found.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    public Conversion? GetByDate(DateTime date)
    {
        return this._conversions.FirstOrDefault(c => c.MatchesDate(date));
    }
}

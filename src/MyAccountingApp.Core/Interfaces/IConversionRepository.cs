using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Defines methods for storing and retrieving currency conversions.
/// </summary>
public interface IConversionRepository
{
    /// <summary>
    /// Adds a new currency conversion to the repository.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    void Add(Conversion conversion);

    /// <summary>
    /// Gets the currency conversion for the specified date, or null if not found.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    Conversion? GetByDate(DateTime date);

    /// <summary>
    /// Determines whether a conversion exists for the specified date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if a conversion exists; otherwise, false.</returns>
    bool ExistsForDate(DateTime date);

    /// <summary>
    /// Gets all currency conversions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    IEnumerable<Conversion> GetAll();
}

using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Defines methods for storing and retrieving currency conversions.
/// </summary>
public interface IConversionRepository
{
    /// <summary>
    /// Adds a new currency conversion or updates an existing conversion to the repository.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    public void AddOrUpdate(Conversion conversion);

    /// <summary>
    /// Gets the currency conversion for the specified date, or null if not found.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    public Conversion? GetByDate(DateTime date);

    /// <summary>
    /// Initializes the repository with a collection of conversions.
    /// </summary>
    /// <param name="conversions">The transaction to add.</param>
    public void Initialize(IEnumerable<Conversion> conversions);

    /// <summary>
    /// Gets all currency conversions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    public IEnumerable<Conversion> GetAll();
}

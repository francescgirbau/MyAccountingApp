using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Resolves a currency conversion for a date using the exact stored rate,
/// falling back to the previous stored rate when it is at most a few days old.
/// </summary>
public static class FxRateResolver
{
    /// <summary>
    /// Maximum number of calendar days a previous stored rate may be used for a missing date.
    /// </summary>
    public const int DefaultMaxLookbackDays = 5;

    /// <summary>
    /// Returns the conversion for the exact date, or the previous stored conversion
    /// when it is within <paramref name="maxLookbackDays"/> calendar days; otherwise null.
    /// </summary>
    /// <param name="repository">The conversion repository to query.</param>
    /// <param name="date">The requested date.</param>
    /// <param name="maxLookbackDays">Maximum allowed gap in calendar days to the previous rate.</param>
    /// <returns>The resolved conversion, or null when no rate is close enough.</returns>
    public static Conversion? Resolve(IConversionRepository repository, DateOnly date, int maxLookbackDays = DefaultMaxLookbackDays)
    {
        DateTime day = date.ToDateTime(TimeOnly.MinValue);
        Conversion? exact = repository.GetByDate(day);
        if (exact is not null)
        {
            return exact;
        }

        Conversion? previous = repository.GetLatestOnOrBefore(day);
        if (previous is null)
        {
            return null;
        }

        int gapDays = date.DayNumber - DateOnly.FromDateTime(previous.Date).DayNumber;
        return gapDays <= maxLookbackDays ? previous : null;
    }
}
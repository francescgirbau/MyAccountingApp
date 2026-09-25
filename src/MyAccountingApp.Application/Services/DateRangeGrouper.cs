namespace MyAccountingApp.Application.Services;

/// <summary>
/// Groups sorted dates into contiguous ranges, each capped at a maximum number of days.
/// </summary>
public static class DateRangeGrouper
{
    /// <summary>
    /// Groups the given dates into contiguous ranges where consecutive days are merged
    /// into a single range as long as it stays within <paramref name="maxDays"/>.
    /// </summary>
    /// <param name="dates">The dates to group (order does not matter).</param>
    /// <param name="maxDays">The maximum number of days a single range may cover.</param>
    /// <returns>The contiguous ranges, as (start, end) pairs.</returns>
    public static List<(DateOnly Start, DateOnly End)> GroupIntoRanges(List<DateOnly> dates, int maxDays)
    {
        List<(DateOnly, DateOnly)> ranges = new();

        if (dates.Count == 0)
        {
            return ranges;
        }

        List<DateOnly> ordered = dates.Distinct().OrderBy(d => d).ToList();

        DateOnly rangeStart = ordered[0];
        DateOnly rangeEnd = ordered[0];

        for (int i = 1; i < ordered.Count; i++)
        {
            DateOnly day = ordered[i];
            bool consecutive = day.AddDays(-1) <= rangeEnd;
            bool fits = day.DayNumber - rangeStart.DayNumber < maxDays;

            if (consecutive && fits)
            {
                rangeEnd = day;
            }
            else
            {
                ranges.Add((rangeStart, rangeEnd));
                rangeStart = day;
                rangeEnd = day;
            }
        }

        ranges.Add((rangeStart, rangeEnd));
        return ranges;
    }
}
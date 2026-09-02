using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Provides deterministic helpers used to resolve duplicate transactions.
/// </summary>
public static class DuplicateResolutionService
{
    /// <summary>
    /// Picks the transaction to keep from a set of duplicates that share the same fingerprint.
    /// </summary>
    /// <param name="duplicates">The candidate duplicate transactions.</param>
    /// <returns>The transaction that should be kept by default.</returns>
    /// <exception cref="ArgumentException">Thrown when no candidates are provided.</exception>
    public static Transaction PickDefaultKeeper(IReadOnlyList<Transaction> duplicates)
    {
        return Pick(
            duplicates,
            t => t.Source,
            t => t.Id);
    }

    /// <summary>
    /// Picks the asset transaction to keep from a set of duplicates that share the same fingerprint.
    /// </summary>
    /// <param name="duplicates">The candidate duplicate asset transactions.</param>
    /// <returns>The asset transaction that should be kept by default.</returns>
    /// <exception cref="ArgumentException">Thrown when no candidates are provided.</exception>
    public static AssetTransaction PickDefaultKeeper(IReadOnlyList<AssetTransaction> duplicates)
    {
        return Pick(
            duplicates,
            a => a.Source ?? a.Transaction.Source,
            a => a.Transaction.Id);
    }

    private static T Pick<T>(IReadOnlyList<T> items, Func<T, string?> sourceSelector, Func<T, Guid> idSelector)
    {
        if (items is null || items.Count == 0)
        {
            throw new ArgumentException("At least one duplicate candidate is required.", nameof(items));
        }

        return items
            .OrderByDescending(item => HasProvenance(sourceSelector(item)))
            .ThenBy(item => idSelector(item))
            .First();
    }

    private static bool HasProvenance(string? source) =>
        !string.IsNullOrWhiteSpace(source);
}

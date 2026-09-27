using MyAccountingApp.Contracts;

namespace MyAccountingApp.Web.Services;

/// <summary>
/// Provides deterministic helpers used to resolve duplicate transactions.
/// </summary>
public static class DuplicateResolutionService
{
    /// <summary>
    /// Picks the transaction to keep from a set of duplicates that share the same fingerprint.
    /// Prefers rows with provenance (an import source), then the smallest id for determinism.
    /// </summary>
    /// <param name="duplicates">The candidate duplicate transactions.</param>
    /// <returns>The transaction that should be kept by default.</returns>
    /// <exception cref="ArgumentException">Thrown when no candidates are provided.</exception>
    public static TransactionDto PickDefaultKeeper(IReadOnlyList<TransactionDto> duplicates)
    {
        if (duplicates is null || duplicates.Count == 0)
        {
            throw new ArgumentException("At least one duplicate candidate is required.", nameof(duplicates));
        }

        return duplicates
            .OrderByDescending(item => HasProvenance(item.Source))
            .ThenBy(item => item.Id)
            .First();
    }

    private static bool HasProvenance(string? source) =>
        !string.IsNullOrWhiteSpace(source);
}
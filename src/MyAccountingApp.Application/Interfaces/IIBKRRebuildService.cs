using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Prepares and executes the Interactive Brokers rebuild: removing the rows imported
/// from IBKR statements for the requested years so they can be re-imported cleanly
/// with the current parser.
/// </summary>
public interface IIBKRRebuildService
{
    /// <summary>Computes what an IBKR rebuild would delete for the given years.</summary>
    IBKRRebuildPreviewDto Preview(IReadOnlyCollection<int> years);

    /// <summary>
    /// Writes a backup of the current store, removes the IBKR rows of the given years
    /// (and option rows without a recorded source) and returns the deletion result.
    /// </summary>
    IBKRRebuildResultDto Rebuild(IReadOnlyCollection<int> years);
}
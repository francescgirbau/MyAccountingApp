namespace MyAccountingApp.Application.Interfaces;

using MyAccountingApp.Contracts;

/// <summary>
/// Computes per-contract option positions from the stored option transactions.
/// </summary>
public interface IOptionPositionService
{
    /// <summary>
    /// Aggregates the option transactions into per-contract positions.
    /// </summary>
    /// <returns>The positions, ordered by open date (newest first).</returns>
    IReadOnlyList<OptionPositionDto> GetPositions();
}
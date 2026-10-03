namespace MyAccountingApp.Application.Interfaces;

using MyAccountingApp.Contracts;

/// <summary>
/// Provides a read-only diagnostic of the data coming from Interactive Brokers statements.
/// </summary>
public interface IIBKRDataHealthService
{
    /// <summary>
    /// Builds the IBKR data health report from the repositories.
    /// </summary>
    /// <returns>
    /// The report with per-year and per-source counts, expired-but-open option contracts
    /// and phantom asset candidates.
    /// </returns>
    IBKRDataHealthDto GetReport();
}
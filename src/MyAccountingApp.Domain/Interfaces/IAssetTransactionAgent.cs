using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Defines the contract for a asset transaction agent that converts broker data
/// into standardized Asset Transaction objects.
/// </summary>
public interface IAssetTransactionAgent
{
    /// <summary>
    /// Reads a file and converts its contents into standardized asset transactions.
    /// </summary>
    /// <param name="filePath">Path to the CSV or Excel file.</param>
    /// <returns>List of standardized asset transactions.</returns>
    Task<IEnumerable<AssetTransaction>> ParseAssetTransactionsAsync(string filePath);
}

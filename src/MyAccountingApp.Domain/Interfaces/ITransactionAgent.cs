using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces
{
    /// <summary>
    /// Defines the contract for a transaction agent that converts bank or broker data (which are not related to assets)
    /// into standardized Transaction objects.
    /// </summary>
    public interface ITransactionAgent
    {
        /// <summary>
        /// Reads a file and converts its contents into standardized transactions.
        /// </summary>
        /// <param name="filePath">Path to the CSV or Excel file.</param>
        /// <returns>List of standardized transactions.</returns>
        public Task<IEnumerable<Transaction>> ParseTransactionsAsync(string filePath);

    }
}

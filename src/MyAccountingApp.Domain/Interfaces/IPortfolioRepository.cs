using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces
{
    public interface IPortfolioRepository
    {
        /// <summary>
        /// Adds an asset transaction to the portfolio.
        /// </summary>
        /// <param name="assetTransaction">The asset transaction to add.</param>
        public void AddOrUpdate(AssetTransaction assetTransaction);

        /// <summary>
        /// Gets all asset transactions for a specific asset symbol.
        /// </summary>
        /// <param name="symbol">The ticker symbol of the asset.</param>
        /// <returns></returns>
        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol);

        /// <summary>
        /// Returns all asset transactions in the portfolio.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AssetTransaction> GetAllTransactions();

        /// <summary>
        /// Deletes an asset transaction by its transaction ID.
        /// </summary>
        /// <param name="transactionId">The transaction ID of the asset transaction to delete.</param>
        /// <returns>True if the transaction was found and removed; otherwise, false.</returns>
        public bool Delete(Guid transactionId);

        /// <summary>
        /// Initializes the repository with a collection of asset transactions.
        /// </summary>
        /// <param name="transactions">A collection of asset transactions to initialize the repository.</param>
        public void Initialize(IEnumerable<AssetTransaction> transactions);

        /// <summary>
        /// Deletes all asset transactions for the specified year.
        /// </summary>
        /// <param name="year">The year to delete transactions for.</param>
        /// <returns>The number of asset transactions deleted.</returns>
        public int DeleteByYear(int year);
    }
}

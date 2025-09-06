using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces
{
    public interface IPortfolioRepository
    {
        /// <summary>
        /// Adds an asset transaction to the portfolio.
        /// </summary>
        /// <param name="assetTransaction">The asset transaction to add.</param>
        void AddOrUpdate(AssetTransaction assetTransaction);

        /// <summary>
        /// Gets all asset transactions for a specific asset symbol.
        /// </summary>
        /// <param name="symbol">The ticker symbol of the asset.</param>
        /// <returns></returns>
        IEnumerable<AssetTransaction> GetAssetTransactions(string symbol);

        /// <summary>
        /// Returns all asset transactions in the portfolio.
        /// </summary>
        /// <returns></returns>
        IEnumerable<AssetTransaction> GetAllTransactions();

        /// <summary>
        /// Initializes the repository with a collection of asset transactions.
        /// </summary>
        /// <param name="transactions">A collection of asset transactions to initialize the repository.</param>
        void Initialize(IEnumerable<AssetTransaction> transactions);
    }
}

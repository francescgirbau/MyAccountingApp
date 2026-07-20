using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Repositories
{
    public class InMemoryPortfolioRepository : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _assetTransactions = new();

        public void AddOrUpdate(AssetTransaction assetTransaction)
        {
            this._assetTransactions.RemoveAll(t => t.Transaction.Id == assetTransaction.Transaction.Id);
            this._assetTransactions.Add(assetTransaction);
        }

        public bool Delete(Guid transactionId)
        {
            return this._assetTransactions.RemoveAll(t => t.Transaction.Id == transactionId) > 0;
        }

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol)
        {
            return this._assetTransactions.Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<AssetTransaction> GetAllTransactions() => this._assetTransactions;

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._assetTransactions.Clear();
            this._assetTransactions.AddRange(transactions);
        }

        public int DeleteByYear(int year)
        {
            return this._assetTransactions.RemoveAll(a => a.Transaction.Date.Year == year);
        }
    }
}

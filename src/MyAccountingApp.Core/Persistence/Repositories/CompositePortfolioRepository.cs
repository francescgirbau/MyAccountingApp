using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence
{
    public class CompositePortfolioRepository : IPortfolioRepository
    {
        private readonly InMemoryPortfolioRepository _memoryRepo;
        private readonly JsonPortfolioRepository _jsonRepo;

        public CompositePortfolioRepository(string jsonPath)
        {
            this._jsonRepo = new JsonPortfolioRepository(jsonPath);
            this._memoryRepo = new InMemoryPortfolioRepository();

            List<AssetTransaction> transactions = this._jsonRepo.GetAllTransactions().ToList();
            this._memoryRepo.Initialize(transactions);
        }

        public void AddOrUpdate(AssetTransaction assetTransaction)
        {
            this._memoryRepo.AddOrUpdate(assetTransaction);
            this._jsonRepo.AddOrUpdate(assetTransaction);
        }

        public bool Delete(Guid transactionId)
        {
            this._jsonRepo.Delete(transactionId);
            return this._memoryRepo.Delete(transactionId);
        }

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol)
        {
            return this._memoryRepo.GetAssetTransactions(symbol);
        }

        public IEnumerable<AssetTransaction> GetAllTransactions()
        {
            return this._memoryRepo.GetAllTransactions();
        }

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._memoryRepo.Initialize(transactions);
            this._jsonRepo.Initialize(transactions);
        }

        public int DeleteByYear(int year)
        {
            int jsonRemoved = this._jsonRepo.DeleteByYear(year);
            int memoryRemoved = this._memoryRepo.DeleteByYear(year);
            return Math.Max(jsonRemoved, memoryRemoved);
        }
    }
}

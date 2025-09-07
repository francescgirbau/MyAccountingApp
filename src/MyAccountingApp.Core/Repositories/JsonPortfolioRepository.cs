using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Repositories
{
    public class JsonPortfolioRepository : IPortfolioRepository
    {
        private readonly string _filePath;

        public JsonPortfolioRepository(string filePath)
        {
            this._filePath = filePath;
        }

        public void AddOrUpdate(AssetTransaction assetTransaction)
        {
            List<AssetTransaction> transactions = this.GetAllTransactions().ToList();
            transactions.RemoveAll(t => t.Transaction.Id == assetTransaction.Transaction.Id);
            transactions.Add(assetTransaction);
            this.Initialize(transactions);
        }

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol)
        {
            return this.GetAllTransactions()
                .Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<AssetTransaction> GetAllTransactions()
        {
            if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
            {
                string json = File.ReadAllText(this._filePath);
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                };

                List<AssetTransaction>? transactions = JsonSerializer.Deserialize<List<AssetTransaction>>(json, options);
                if (transactions != null)
                {
                    return transactions;
                }
            }

            return new List<AssetTransaction>();
        }

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            };
            string json = JsonSerializer.Serialize(transactions, options);

            File.WriteAllText(this._filePath, json);
        }
    }
}

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
            this.WriteAll(transactions);
        }

        public bool Delete(Guid transactionId)
        {
            List<AssetTransaction> transactions = this.GetAllTransactions().ToList();
            int removed = transactions.RemoveAll(t => t.Transaction.Id == transactionId);
            if (removed > 0)
            {
                this.WriteAll(transactions);
            }

            return removed > 0;
        }

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol)
        {
            return this.GetAllTransactions()
                .Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<AssetTransaction> GetAllTransactions()
        {
            if (!File.Exists(this._filePath) || new FileInfo(this._filePath).Length == 0)
            {
                return new List<AssetTransaction>();
            }

            string json = File.ReadAllText(this._filePath);
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
            };

            try
            {
                return JsonSerializer.Deserialize<List<AssetTransaction>>(json, options) ?? new List<AssetTransaction>();
            }
            catch (JsonException)
            {
                int lastBrace = json.LastIndexOf('}');
                if (lastBrace > 0)
                {
                    string repaired = json[.. (lastBrace + 1)] + "]";
                    try
                    {
                        List<AssetTransaction>? recovered = JsonSerializer.Deserialize<List<AssetTransaction>>(repaired, options);
                        if (recovered is not null)
                        {
                            this.WriteAll(recovered);
                            return recovered;
                        }
                    }
                    catch
                    {
                    }
                }

                return new List<AssetTransaction>();
            }
        }

        public void Initialize(IEnumerable<AssetTransaction> transactions) => this.WriteAll(transactions);

        public int DeleteByYear(int year)
        {
            List<AssetTransaction> all = this.GetAllTransactions().ToList();
            int removed = all.RemoveAll(a => a.Transaction.Date.Year == year);
            if (removed > 0)
            {
                this.WriteAll(all);
            }

            return removed;
        }

        private void WriteAll(IEnumerable<AssetTransaction> transactions)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            };
            string json = JsonSerializer.Serialize(transactions, options);
            string tempPath = this._filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, this._filePath, overwrite: true);
        }
    }
}

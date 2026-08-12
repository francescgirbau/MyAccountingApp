using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

public class JsonTransactionRepository : ITransactionRepository
{
    private readonly string _filePath;
    private readonly IVaultService? _vaultService;

    public JsonTransactionRepository(string filePath, IVaultService? vaultService = null)
    {
        this._filePath = filePath;
        this._vaultService = vaultService;
    }

    public void AddOrUpdate(Transaction transaction)
    {
        List<Transaction> transactions = this.GetAll().ToList();
        transactions.RemoveAll(tx => tx.Id == transaction.Id);
        transactions.Add(transaction);
        this.WriteAll(transactions);
    }

    public bool Delete(Transaction transaction)
    {
        List<Transaction> transactions = this.GetAll().ToList();

        if (!transactions.Any(tx => tx.Id == transaction.Id))
        {
            return false;
        }

        transactions.RemoveAll(tx => tx.Id == transaction.Id);
        this.WriteAll(transactions);

        return true;
    }

    public IEnumerable<Transaction> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this._filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Transaction>();
        }

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        try
        {
            List<Transaction>? transactions = JsonSerializer.Deserialize<List<Transaction>>(json, options);
            if (transactions is null || transactions.Count == 0)
            {
                return new List<Transaction>();
            }

            var seen = new HashSet<Guid>();
            var deduplicated = new List<Transaction>(transactions.Count);
            for (int i = transactions.Count - 1; i >= 0; i--)
            {
                if (seen.Add(transactions[i].Id))
                {
                    deduplicated.Add(transactions[i]);
                }
            }

            deduplicated.Reverse();
            if (deduplicated.Count != transactions.Count)
            {
                this.WriteAll(deduplicated);
            }

            return deduplicated;
        }
        catch (JsonException)
        {
            int lastBrace = json.LastIndexOf('}');
            if (lastBrace > 0)
            {
                string repaired = json[.. (lastBrace + 1)] + "]";
                try
                {
                    List<Transaction>? recovered = JsonSerializer.Deserialize<List<Transaction>>(repaired, options);
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

            return new List<Transaction>();
        }
    }

    public int DeleteByYear(int year)
    {
        List<Transaction> transactions = this.GetAll().ToList();
        int removed = transactions.RemoveAll(tx => tx.Date.Year == year);
        if (removed > 0)
        {
            this.WriteAll(transactions);
        }

        return removed;
    }

    public void Initialize(IEnumerable<Transaction> transactions) => this.WriteAll(transactions);

    private void WriteAll(IEnumerable<Transaction> transactions)
    {
        JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        string json = JsonSerializer.Serialize(transactions, options);
        EncryptedJsonFileStorage.WriteText(this._filePath, json, this._vaultService);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Repositories;

public class JsonTransactionRepository : ITransactionRepository
{
    private readonly string _filePath;

    public JsonTransactionRepository(string filePath)
    {
        this._filePath = filePath;
    }

    public void AddOrUpdate(Transaction transaction)
    {
        List<Transaction> transactions = this.GetAll().ToList();
        _ = this.Delete(transaction);
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
        if (!File.Exists(this._filePath) || new FileInfo(this._filePath).Length == 0)
        {
            return new List<Transaction>();
        }

        string json = File.ReadAllText(this._filePath);

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        try
        {
            return JsonSerializer.Deserialize<List<Transaction>>(json, options) ?? new List<Transaction>();
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

    public void Initialize(IEnumerable<Transaction> transactions) => this.WriteAll(transactions);

    private void WriteAll(IEnumerable<Transaction> transactions)
    {
        JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        string json = JsonSerializer.Serialize(transactions, options);
        string tempPath = this._filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, this._filePath, overwrite: true);
    }
}

using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyAccountingApp.Infrastructure.Repositories;

public class JsonTransactionRepository : ITransactionRepository
{

    private readonly string _filePath;
    private readonly List<Transaction> _transactions;

    public JsonTransactionRepository(string filePath)
    {
        _filePath = filePath;

        if (File.Exists(_filePath) && new FileInfo(_filePath).Length > 0)
        {
            var json = File.ReadAllText(_filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            _transactions = JsonSerializer.Deserialize<List<Transaction>>(json, options) ?? new List<Transaction>();
        }
        else
        {
            _transactions = new List<Transaction>();
        }
    }

    public void Add(Transaction transaction)
    {
        _transactions.Add(transaction);
        Save();
    }

    public void Delete(Guid id)
    {
        var tx = GetTransaction(id);
        if (tx != null)
        {
            _transactions.Remove(tx);
            Save();
        }
    }

    public IEnumerable<Transaction> GetAll()
    {
       return _transactions;
    }

    public Transaction? GetTransaction(Guid id)
    {
        return _transactions.FirstOrDefault(t => t.Id == id);
    }

    private void Save()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(_transactions, options);
        File.WriteAllText(_filePath, json);
    }
}

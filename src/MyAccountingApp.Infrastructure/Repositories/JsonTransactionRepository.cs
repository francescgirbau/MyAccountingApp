using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// Repository for storing and retrieving transactions using a JSON file.
/// </summary>
public class JsonTransactionRepository : ITransactionRepository
{
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTransactionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    public JsonTransactionRepository(string filePath)
    {
        this._filePath = filePath;
    }

    /// <summary>
    /// Adds a new transaction to the repository.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void AddOrUpdate(Transaction transaction)
    {
        List<Transaction> transactions = this.GetAll().ToList();
        _ = this.Delete(transaction);
        transactions.Add(transaction);
        this.Initialize(transactions);
    }

    /// <summary>
    /// Deletes a transaction from the repository by its unique identifier.
    /// </summary>
    /// <param name="transaction">The transaction to delete.</param>
    /// <returns>True if the transaction was found and removed; otherwise, false.</returns> 
    public bool Delete(Transaction transaction)
    {
        List<Transaction> transactions = this.GetAll().ToList();

        if (!transactions.Any(tx => tx.Id == transaction.Id))
        {
            return false;
        }

        transactions.RemoveAll(tx => tx.Id == transaction.Id);
        this.Initialize(transactions);

        return true;
    }

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll()
    {
        if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
        {
            string json = File.ReadAllText(this._filePath);

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
            };

            List<Transaction>? transactions = JsonSerializer.Deserialize<List<Transaction>>(json, options);

            if (transactions is not null)
            {
                return transactions;
            }
        }

        return new List<Transaction>();
    }

    /// <summary>
    /// Saves the transactions to the JSON file.
    /// </summary>
    /// <param name="transactions">The transactions to save.</param> 
    public void Initialize(IEnumerable<Transaction> transactions)
    {
        JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, };

        string json = JsonSerializer.Serialize(transactions, options);
        File.WriteAllText(this._filePath, json);
    }
}

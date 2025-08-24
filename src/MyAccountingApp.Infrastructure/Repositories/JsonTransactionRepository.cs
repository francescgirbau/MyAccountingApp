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
    private readonly List<Transaction> _transactions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTransactionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    public JsonTransactionRepository(string filePath)
    {
        this._filePath = filePath;

        if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
        {
            string json = File.ReadAllText(this._filePath);

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
            };

            this._transactions = JsonSerializer.Deserialize<List<Transaction>>(json, options) ?? new List<Transaction>();
        }
        else
        {
            this._transactions = new List<Transaction>();
        }
    }

    /// <summary>
    /// Adds a new transaction to the repository.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void Add(Transaction transaction)
    {
        this._transactions.Add(transaction);
        this.Save();
    }

    /// <summary>
    /// Deletes a transaction from the repository by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to delete.</param>
    public void Delete(Guid id)
    {
        Transaction? tx = this.GetTransaction(id);
        if (tx != null)
        {
            this._transactions.Remove(tx);
            this.Save();
        }
    }

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll()
    {
        return this._transactions;
    }

    /// <summary>
    /// Gets a transaction by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <returns>The transaction if found; otherwise, null.</returns>
    public Transaction? GetTransaction(Guid id)
    {
        return this._transactions.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// Saves the transactions to the JSON file.
    /// </summary>
    private void Save()
    {
        JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, };

        string json = JsonSerializer.Serialize(this._transactions, options);
        File.WriteAllText(this._filePath, json);
    }
}

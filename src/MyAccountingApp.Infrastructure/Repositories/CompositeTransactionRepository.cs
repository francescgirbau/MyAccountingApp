using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// Repository that combines in-memory and JSON-backed transaction storage.
/// </summary>
public class CompositeTransactionRepository : ITransactionRepository
{
    private readonly InMemoryTransactionRepository _memoryRepo;
    private readonly JsonTransactionRepository _jsonRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTransactionRepository"/> class.
    /// </summary>
    /// <param name="jsonPath">The path to the JSON file for persistent storage.</param>
    public CompositeTransactionRepository(string jsonPath)
    {
        this._jsonRepo = new JsonTransactionRepository(jsonPath);
        this._memoryRepo = new InMemoryTransactionRepository();

        foreach (Transaction transaction in this._jsonRepo.GetAll())
        {
            this._memoryRepo.Add(transaction);
        }
    }

    /// <summary>
    /// Adds a transaction to both in-memory and JSON repositories.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void Add(Transaction transaction)
    {
        this._memoryRepo.Add(transaction);
        this._jsonRepo.Add(transaction);
    }

    /// <summary>
    /// Deletes a transaction from both in-memory and JSON repositories.
    /// </summary>
    /// <param name="id">The ID of the transaction to delete.</param>
    public void Delete(Guid id)
    {
        this._memoryRepo.Delete(id);
        this._jsonRepo.Delete(id);
    }

    /// <summary>
    /// Gets all transactions from the in-memory repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll()
    {
        return this._memoryRepo.GetAll();
    }

    /// <summary>
    /// Gets a transaction by ID from the in-memory repository.
    /// </summary>
    /// <param name="id">The ID of the transaction.</param>
    /// <returns>The transaction if found; otherwise, null.</returns>
    public Transaction? GetTransaction(Guid id)
    {
        return this._memoryRepo.GetTransaction(id);
    }
}

using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Repositories;

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

        this._memoryRepo.Initialize(this._jsonRepo.GetAll());
    }

    /// <summary>
    /// Adds a transaction to both in-memory and JSON repositories.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void AddOrUpdate(Transaction transaction)
    {
        this._memoryRepo.AddOrUpdate(transaction);
        this._jsonRepo.AddOrUpdate(transaction);
    }

    /// <summary>
    /// Deletes a transaction from both in-memory and JSON repositories.
    /// </summary>
    /// <param name="transaction">The ID of the transaction to delete.</param>
    /// <returns>True if the transaction was found and removed; otherwise, false.</returns> 
    public bool Delete(Transaction transaction)
    {
        this._jsonRepo.Delete(transaction);
        return this._memoryRepo.Delete(transaction);
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
    /// Initializes the repositories with the provided collection of transactions.
    /// </summary>
    /// <remarks>This method initializes both the in-memory and JSON-based repositories with the same set of
    /// transactions. Ensure that the provided collection contains all necessary transactions before calling this
    /// method.</remarks>
    /// <param name="transactions">A collection of <see cref="Transaction"/> objects to be used for initialization. Cannot be null.</param>
    public void Initialize(IEnumerable<Transaction> transactions)
    {
        this._memoryRepo.Initialize(transactions);
        this._jsonRepo.Initialize(transactions);
    }
}

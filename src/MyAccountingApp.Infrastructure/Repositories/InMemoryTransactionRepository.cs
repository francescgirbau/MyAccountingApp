using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// In-memory repository for storing and managing financial transactions.
/// Intended for fast, non-persistent operations such as testing or caching.
/// </summary>
public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    /// <summary>
    /// Adds a new transaction to the repository.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void Add(Transaction transaction) => this._transactions.Add(transaction);

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll() => this._transactions;

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
    /// Deletes a transaction from the repository by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to delete.</param>
    public void Delete(Guid id)
    {
        Transaction? transaction = this.GetTransaction(id);

        if (transaction != null)
        {
            this._transactions.Remove(transaction);
        }
    }
}
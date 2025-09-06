using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Repositories;

/// <summary>
/// In-memory repository for storing and managing financial transactions.
/// Intended for fast, non-persistent operations such as testing or caching.
/// </summary>
public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    /// <summary>
    /// Adds a new transaction to the repository.
    /// In case of a duplicate ID, the existing transaction is replaced.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void AddOrUpdate(Transaction transaction)
    {
        _ = this.Delete(transaction);

        this._transactions.Add(transaction);
    }

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll() => this._transactions;

    /// <summary>
    /// Deletes a transaction from the repository by its unique identifier.
    /// </summary>
    /// <param name="transaction">The transaction to delete.</param>
    /// <returns>True if the transaction was found and removed; otherwise, false.</returns>
    public bool Delete(Transaction transaction)
    {
        return this._transactions.RemoveAll(tx => tx.Id == transaction.Id) > 0;
    }

    /// <summary>
    /// The Initialize method clears any existing transactions and populates the repository with the provided collection.
    /// </summary>
    /// <param name="transactions">The transactions to initialize the repository with.</param>
    public void Initialize(IEnumerable<Transaction> transactions)
    {
        this._transactions.Clear();
        this._transactions.AddRange(transactions);
    }
}

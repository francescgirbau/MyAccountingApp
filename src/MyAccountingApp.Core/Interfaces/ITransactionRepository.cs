using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Defines methods for storing and managing financial transactions.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Initializes the repository with a collection of transactions.
    /// </summary>
    /// <param name="transactions">The transaction to add.</param>
    public void Initialize(IEnumerable<Transaction> transactions);

    /// <summary>
    /// Adds or update a transaction to the repository.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    public void AddOrUpdate(Transaction transaction);

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    public IEnumerable<Transaction> GetAll();

    /// <summary>
    /// Deletes a transaction from the repository.
    /// </summary>
    /// <param name="transaction">The transaction to delete.</param>
    /// <returns>True if the transaction was found and removed; otherwise, false.</returns>
    public bool Delete(Transaction transaction);
}

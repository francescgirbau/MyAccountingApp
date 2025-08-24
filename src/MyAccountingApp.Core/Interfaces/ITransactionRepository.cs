using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Defines methods for storing and managing financial transactions.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Adds a new transaction to the repository.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    void Add(Transaction transaction);

    /// <summary>
    /// Gets all transactions stored in the repository.
    /// </summary>
    /// <returns>An enumerable of all transactions.</returns>
    IEnumerable<Transaction> GetAll();

    /// <summary>
    /// Gets a transaction by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <returns>The transaction if found; otherwise, null.</returns>
    Transaction? GetTransaction(Guid id);

    /// <summary>
    /// Deletes a transaction from the repository by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to delete.</param>
    void Delete(Guid id);
}
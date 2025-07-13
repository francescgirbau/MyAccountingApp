using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    public void Add(Transaction transaction) => _transactions.Add(transaction);

    public IEnumerable<Transaction> GetAll() => _transactions;

    public Transaction? GetTransaction(Guid id) =>
        _transactions.FirstOrDefault(t => t.Id == id);

    public void Delete(Guid id)
    {
        var transaction = GetTransaction(id);
        if (transaction != null)
            _transactions.Remove(transaction);
    }

}
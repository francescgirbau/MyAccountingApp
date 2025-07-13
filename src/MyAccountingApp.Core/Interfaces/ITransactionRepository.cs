using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

public interface ITransactionRepository
{
    void Add(Transaction transaction);
    IEnumerable<Transaction> GetAll();
    Transaction? GetTransaction(Guid id);
    void Delete(Guid id);
}


using _1.Core.Entities;

namespace _1.Core.Interfaces
{
    public interface ITransactionRepository
    {
        void Add(Transaction transaction);
        IEnumerable<Transaction> GetAll();
        Transaction? GetTransaction(Guid id);
        void Delete(Guid id);
        double GetBalance();
        




    }
}

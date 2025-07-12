using _1.Core.Entities;

namespace _1.Core.Interfaces
{
    public interface ITransactionRepository
    {
        void Add(Transaction transaction);
        IEnumerable<Transaction> GetAll();
        decimal GetBalance();
        




    }
}

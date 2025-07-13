namespace MyAccountingApp.Core;

public interface ITransactionRepository
{
    void Add(Transaction transaction);
    IEnumerable<Transaction> GetAll();
    Transaction? GetTransaction(Guid id);
    void Delete(Guid id);
    double GetBalance();
        


}


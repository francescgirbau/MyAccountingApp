namespace MyAccountingApp.Infrastructure;

public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    public void Add(Transaction transaction) => this._transactions.Add(transaction);

    public IEnumerable<Transaction> GetAll() => this._transactions;

    public Transaction? GetTransaction(Guid id)
    {
        return this._transactions.FirstOrDefault(t => t.Id == id);
    }

    public void Delete(Guid id)
    {
        Transaction transaction = GetTransaction(id);
        if (transaction != null)
        {
            this._transactions.Remove(transaction);
        }
    }

    public double GetBalance() => this._transactions.Sum(t => t.Money.Amount);

}
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeOptionRepository : IOptionTransactionRepository
{
    private readonly List<OptionTransaction> _transactions = new();

    public IEnumerable<OptionTransaction> GetAll() => this._transactions;

    public void Add(OptionTransaction transaction) => this._transactions.Add(transaction);

    public void Update(OptionTransaction transaction)
    {
        this._transactions.RemoveAll(t => t.Transaction.Id == transaction.Transaction.Id);
        this._transactions.Add(transaction);
    }

    public bool Delete(Guid id) => this._transactions.RemoveAll(t => t.Transaction.Id == id) > 0;

    public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);

    public void Initialize(IEnumerable<OptionTransaction> transactions)
    {
        this._transactions.Clear();
        this._transactions.AddRange(transactions);
    }
}
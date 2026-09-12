using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeLoanRepository : ILoanRepository
{
    private readonly List<Loan> _loans = new();

    public IEnumerable<Loan> GetAll() => this._loans;

    public Loan? GetById(Guid id) => this._loans.FirstOrDefault(l => l.Id == id);

    public void Add(Loan loan) => this._loans.Add(loan);

    public void Update(Loan loan)
    {
        this._loans.RemoveAll(l => l.Id == loan.Id);
        this._loans.Add(loan);
    }

    public bool Delete(Guid id) => this._loans.RemoveAll(l => l.Id == id) > 0;

    public void Initialize(IEnumerable<Loan> loans)
    {
        this._loans.Clear();
        this._loans.AddRange(loans);
    }
}
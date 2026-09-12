using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.TestUtilities.Fakes;

public class FakeLoanMovementRepository : ILoanMovementRepository
{
    private readonly List<LoanMovement> _movements = new();

    public IEnumerable<LoanMovement> GetAll() => this._movements;

    public IEnumerable<LoanMovement> GetByLoan(Guid loanId) => this._movements.Where(m => m.LoanId == loanId);

    public void Add(LoanMovement movement) => this._movements.Add(movement);

    public void Update(LoanMovement movement)
    {
        this._movements.RemoveAll(m => m.Id == movement.Id);
        this._movements.Add(movement);
    }

    public bool Delete(Guid id) => this._movements.RemoveAll(m => m.Id == id) > 0;

    public int DeleteByYear(int year) => this._movements.RemoveAll(m => m.Transaction.Date.Year == year);

    public void Initialize(IEnumerable<LoanMovement> movements)
    {
        this._movements.Clear();
        this._movements.AddRange(movements);
    }
}
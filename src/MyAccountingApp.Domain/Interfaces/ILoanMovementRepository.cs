using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

public interface ILoanMovementRepository
{
    IEnumerable<LoanMovement> GetAll();

    IEnumerable<LoanMovement> GetByLoan(Guid loanId);

    void Add(LoanMovement movement);

    void Update(LoanMovement movement);

    bool Delete(Guid id);

    int DeleteByYear(int year);

    void Initialize(IEnumerable<LoanMovement> movements);
}
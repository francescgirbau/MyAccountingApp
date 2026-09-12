using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

public interface ILoanRepository
{
    IEnumerable<Loan> GetAll();

    Loan? GetById(Guid id);

    void Add(Loan loan);

    void Update(Loan loan);

    bool Delete(Guid id);

    void Initialize(IEnumerable<Loan> loans);
}
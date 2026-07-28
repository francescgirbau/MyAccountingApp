using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

public interface IOptionTransactionRepository
{
    IEnumerable<OptionTransaction> GetAll();

    void Add(OptionTransaction transaction);

    bool Delete(Guid id);

    int DeleteByYear(int year);
}

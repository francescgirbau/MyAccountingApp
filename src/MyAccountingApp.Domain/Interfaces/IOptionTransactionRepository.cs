using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

public interface IOptionTransactionRepository
{
    IEnumerable<OptionTransaction> GetAll();

    void Add(OptionTransaction transaction);

    void Update(OptionTransaction transaction);

    bool Delete(Guid id);

    int DeleteByYear(int year);

    void Initialize(IEnumerable<OptionTransaction> transactions);
}

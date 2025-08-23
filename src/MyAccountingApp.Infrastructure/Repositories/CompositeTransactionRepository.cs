using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

public class CompositeTransactionRepository : ITransactionRepository
{
    private readonly InMemoryTransactionRepository _memoryRepo;
    private readonly JsonTransactionRepository _jsonRepo;

    public CompositeTransactionRepository(string jsonPath)
    {
        _jsonRepo = new JsonTransactionRepository(jsonPath);
        _memoryRepo = new InMemoryTransactionRepository();

        foreach (Transaction transaction in _jsonRepo.GetAll())
        {
            _memoryRepo.Add(transaction);
        }
    }

    public void Add(Transaction transaction)
    {
        _memoryRepo.Add(transaction);
        _jsonRepo.Add(transaction);
    }

    public void Delete(Guid id)
    {
        _memoryRepo.Delete(id);
        _jsonRepo.Delete(id);
    }

    public IEnumerable<Transaction> GetAll()
    {
        return _memoryRepo.GetAll();
    }

    public Transaction? GetTransaction(Guid id)
    {
        return _memoryRepo.GetTransaction(id);
    }
}

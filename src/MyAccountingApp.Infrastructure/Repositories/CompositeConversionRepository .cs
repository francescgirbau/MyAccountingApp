using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

public class CompositeConversionRepository : IConversionRepository
{
    private readonly InMemoryConversionRepository _memoryRepo;
    private readonly JsonConversionRepository _jsonRepo;


    public CompositeConversionRepository(string jsonPath)
    {
        _jsonRepo = new JsonConversionRepository(jsonPath);
        _memoryRepo = new InMemoryConversionRepository();

        // Pre-carrega les conversions existents del JSON a la memòria
        foreach (Conversion conversion in _jsonRepo.GetAll())
        {
            _memoryRepo.Add(conversion);
        }
    }

    public void Add(Conversion conversion)
    {
        _memoryRepo.Add(conversion);
        _jsonRepo.Add(conversion); // Persistència
    }

    public bool ExistsForDate(DateTime date)
    {
        return _memoryRepo.ExistsForDate(date);
    }

    public IEnumerable<Conversion> GetAll()
    {
        return _memoryRepo.GetAll();
    }

    public Conversion? GetByDate(DateTime date)
    {
        return _memoryRepo.GetByDate(date);
    }
}

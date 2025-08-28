using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// Repository that combines in-memory and JSON-backed storage for currency conversions.
/// </summary>
public class CompositeConversionRepository : IConversionRepository
{
    private readonly InMemoryConversionRepository _memoryRepo;
    private readonly JsonConversionRepository _jsonRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeConversionRepository"/> class.
    /// Loads existing conversions from the JSON file into memory.
    /// </summary>
    /// <param name="jsonPath">The path to the JSON file for persistent storage.</param>
    public CompositeConversionRepository(string jsonPath)
    {
        this._jsonRepo = new JsonConversionRepository(jsonPath);
        this._memoryRepo = new InMemoryConversionRepository();

        List<Conversion> conversions = this._jsonRepo.GetAll().ToList();

        this._memoryRepo.Initialize(conversions);
    }

    /// <summary>
    /// Adds a conversion to both in-memory and JSON repositories.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    public void AddOrUpdate(Conversion conversion)
    {
        this._memoryRepo.AddOrUpdate(conversion);
        this._jsonRepo.AddOrUpdate(conversion);
    }

    /// <summary>
    /// Gets all conversions from the in-memory repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    public IEnumerable<Conversion> GetAll()
    {
        return this._memoryRepo.GetAll();
    }

    /// <summary>
    /// Gets the conversion for the specified date from the in-memory repository.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    public Conversion? GetByDate(DateTime date)
    {
        return this._memoryRepo.GetByDate(date);
    }

    /// <summary>
    /// Initializes the repositories with the provided collection of conversions.
    /// <param name="conversions">The list of conversions.</param>
    /// </summary>
    public void Initialize(IEnumerable<Conversion> conversions)
    {
        this._memoryRepo.Initialize(conversions);
        this._jsonRepo.Initialize(conversions);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Repository for storing and retrieving currency conversions using a JSON file.
/// </summary>
public class JsonConversionRepository : IConversionRepository
{
    private readonly string _filePath;
    private readonly IVaultService? _vaultService;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonConversionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    /// <param name="vaultService">Optional vault service for encryption.</param>
    public JsonConversionRepository(string filePath, IVaultService? vaultService = null)
    {
        this._filePath = filePath;
        this._vaultService = vaultService;
    }

    /// <summary>
    /// Adds a new conversion to the repository.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if a conversion for the date already exists.</exception>
    public void AddOrUpdate(Conversion conversion)
    {
        List<Conversion> conversions = this.GetAll().ToList();

        conversions.RemoveAll(c => c.Date == conversion.Date);
        conversions.Add(conversion);
        this.Initialize(conversions);
    }

    /// <summary>
    /// Gets all conversions in the repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    public IEnumerable<Conversion> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this._filePath, this._vaultService);
        if (!string.IsNullOrWhiteSpace(json))
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            List<Conversion>? conversions = JsonSerializer.Deserialize<List<Conversion>>(json, options);

            if (conversions != null)
            {
                return conversions;
            }
        }

        return Enumerable.Empty<Conversion>();
    }

    /// <summary>
    /// Gets the conversion for the specified date, or null if not found.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    public Conversion? GetByDate(DateTime date)
    {
        List<Conversion> conversions = this.GetAll().ToList();

        return conversions.FirstOrDefault(c => c.MatchesDate(date));
    }

    /// <summary>
    /// Gets the most recent conversion on or before the specified date, or null if none exists.
    /// </summary>
    /// <param name="date">The upper bound for the conversion date.</param>
    /// <returns>The latest conversion on or before the date; otherwise, null.</returns>
    public Conversion? GetLatestOnOrBefore(DateTime date)
    {
        List<Conversion> conversions = this.GetAll().ToList();

        return conversions.Where(c => c.Date.Date <= date).MaxBy(c => c.Date);
    }

    public void Initialize(IEnumerable<Conversion> conversions)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(conversions, options);

        EncryptedJsonFileStorage.WriteText(this._filePath, json, this._vaultService);
    }
}

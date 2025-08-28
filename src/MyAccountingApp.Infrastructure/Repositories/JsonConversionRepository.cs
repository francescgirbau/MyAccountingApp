using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;

namespace MyAccountingApp.Infrastructure.Repositories;

/// <summary>
/// Repository for storing and retrieving currency conversions using a JSON file.
/// </summary>
public class JsonConversionRepository : IConversionRepository
{
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonConversionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    public JsonConversionRepository(string filePath)
    {
        this._filePath = filePath;
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
        if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
        {
            string json = File.ReadAllText(this._filePath);
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            List<Conversion>? conversions = JsonSerializer.Deserialize<List<Conversion>>(json, options);

            if (conversions != null)
            {
                return conversions;
            }
        }

        return new List<Conversion>();
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

    public void Initialize(IEnumerable<Conversion> conversions)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(conversions, options);

        File.WriteAllText(this._filePath, json);
    }
}

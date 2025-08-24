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
    private readonly List<Conversion> _conversions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonConversionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    public JsonConversionRepository(string filePath)
    {
        this._filePath = filePath;

        if (File.Exists(this._filePath))
        {
            string json = File.ReadAllText(this._filePath);
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            this._conversions = JsonSerializer.Deserialize<List<Conversion>>(json, options) ?? new List<Conversion>();
        }
        else
        {
            this._conversions = [];
        }
    }

    /// <summary>
    /// Adds a new conversion to the repository.
    /// </summary>
    /// <param name="conversion">The conversion to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if a conversion for the date already exists.</exception>
    public void Add(Conversion conversion)
    {
        if (this._conversions.Any(c => c.MatchesDate(conversion.Date)))
        {
            throw new InvalidOperationException($"Ja existeix una conversió per la data {conversion.Date:yyyy-MM-dd}");
        }

        this._conversions.Add(conversion);
        this.Save();
    }

    /// <summary>
    /// Checks if a conversion exists for the specified date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if a conversion exists; otherwise, false.</returns>
    public bool ExistsForDate(DateTime date)
    {
        return this._conversions.Any(c => c.MatchesDate(date));
    }

    /// <summary>
    /// Gets all conversions in the repository.
    /// </summary>
    /// <returns>An enumerable of all conversions.</returns>
    public IEnumerable<Conversion> GetAll()
    {
        return this._conversions;
    }

    /// <summary>
    /// Gets the conversion for the specified date, or null if not found.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <returns>The conversion if found; otherwise, null.</returns>
    public Conversion? GetByDate(DateTime date)
    {
        return this._conversions.FirstOrDefault(c => c.MatchesDate(date));
    }

    /// <summary>
    /// Saves the conversions to the JSON file.
    /// </summary>
    private void Save()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(this._conversions, options);
        File.WriteAllText(this._filePath, json);
    }
}

using MyAccountingApp.Core.Entities;
using MyAccountingApp.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyAccountingApp.Infrastructure.Repositories;

public class JsonConversionRepository : IConversionRepository
{
    private readonly string _filePath;
    private readonly List<Conversion> _conversions;

    public JsonConversionRepository(string filePath)
    {
        _filePath = filePath;

        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            _conversions = JsonSerializer.Deserialize<List<Conversion>>(json, options) ?? new List<Conversion>();
        }
        else
        {
            _conversions = new List<Conversion>();
        }
    }

    public void Add(Conversion conversion)
    {
        if (_conversions.Any(c => c.MatchesDate(conversion.Date)))
            throw new InvalidOperationException($"Ja existeix una conversió per la data {conversion.Date:yyyy-MM-dd}");

        _conversions.Add(conversion);
        Save();
    }

    public bool ExistsForDate(DateTime date)
    {
        return _conversions.Any(c => c.MatchesDate(date));
    }

    public IEnumerable<Conversion> GetAll()
    {
        return _conversions;
    }

    public Conversion? GetByDate(DateTime date)
    {
        return _conversions.FirstOrDefault(c => c.MatchesDate(date));
    }
    private void Save()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() } 
        };

        var json = JsonSerializer.Serialize(_conversions, options);
        File.WriteAllText(_filePath, json);
    }

}

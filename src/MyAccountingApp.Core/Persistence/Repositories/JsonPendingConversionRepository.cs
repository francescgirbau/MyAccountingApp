using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Repository for storing and retrieving queued conversion requests using a JSON file.
/// </summary>
public class JsonPendingConversionRepository : IPendingConversionRepository
{
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPendingConversionRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    public JsonPendingConversionRepository(string filePath)
    {
        this._filePath = filePath;
    }

    /// <summary>
    /// Gets all queued conversion requests.
    /// </summary>
    /// <returns>All queued conversion requests.</returns>
    public IEnumerable<PendingConversionRequest> GetAll()
    {
        if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
        {
            string json = File.ReadAllText(this._filePath);
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            List<PendingConversionRequest>? requests = JsonSerializer.Deserialize<List<PendingConversionRequest>>(json, options);

            if (requests != null)
            {
                return requests;
            }
        }

        return new List<PendingConversionRequest>();
    }

    /// <summary>
    /// Adds a new request or updates an existing request for the same date.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    public void AddOrUpdate(PendingConversionRequest request)
    {
        List<PendingConversionRequest> requests = this.GetAll().ToList();
        requests.RemoveAll(r => r.Date == request.Date);
        requests.Add(request);
        this.Initialize(requests);
    }

    /// <summary>
    /// Replaces all stored requests with the given collection.
    /// </summary>
    /// <param name="requests">The requests to store.</param>
    public void Initialize(IEnumerable<PendingConversionRequest> requests)
    {
        this.EnsureDirectory();
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(requests, options);
        File.WriteAllText(this._filePath, json);
    }

    private void EnsureDirectory()
    {
        string? directory = Path.GetDirectoryName(this._filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Repository for storing and retrieving the currency API usage quota using a JSON file.
/// </summary>
public class JsonApiQuotaRepository : IApiQuotaRepository
{
    private readonly string _filePath;
    private readonly int _requestsLimit;
    private readonly int _safetyMargin;
    private readonly string _providerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonApiQuotaRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    /// <param name="requestsLimit">The monthly request limit.</param>
    /// <param name="safetyMargin">The number of requests reserved as a safety margin.</param>
    /// <param name="providerName">The name of the external provider.</param>
    public JsonApiQuotaRepository(string filePath, int requestsLimit = 100, int safetyMargin = 10, string providerName = "exchangerate.host")
    {
        this._filePath = filePath;
        this._requestsLimit = requestsLimit;
        this._safetyMargin = safetyMargin;
        this._providerName = providerName;
    }

    /// <summary>
    /// Gets the current quota, creating a default quota for the current month if none is stored.
    /// </summary>
    /// <returns>The current quota.</returns>
    public ApiUsageQuota Get()
    {
        if (File.Exists(this._filePath) && new FileInfo(this._filePath).Length > 0)
        {
            string json = File.ReadAllText(this._filePath);
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            ApiUsageQuota? quota = JsonSerializer.Deserialize<ApiUsageQuota>(json, options);

            if (quota != null)
            {
                return quota;
            }
        }

        return this.CreateDefault();
    }

    /// <summary>
    /// Saves the quota to the JSON file.
    /// </summary>
    /// <param name="quota">The quota to save.</param>
    public void Save(ApiUsageQuota quota)
    {
        this.EnsureDirectory();
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(quota, options);
        File.WriteAllText(this._filePath, json);
    }

    private ApiUsageQuota CreateDefault()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly periodStart = new(today.Year, today.Month, 1);
        DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return new ApiUsageQuota(this._providerName, periodStart, periodEnd, 0, this._requestsLimit, this._safetyMargin, DateTime.UtcNow);
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

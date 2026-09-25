using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Repository for storing and retrieving queued work requests using a JSON file.
/// Detects the legacy conversion-only format (dates keyed per request) and migrates
/// it to the generic pending work format on load.
/// </summary>
public class JsonPendingWorkRepository : IPendingWorkRepository
{
    private readonly string _filePath;
    private readonly IVaultService? _vaultService;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPendingWorkRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file.</param>
    /// <param name="vaultService">Optional vault service for encryption.</param>
    public JsonPendingWorkRepository(string filePath, IVaultService? vaultService = null)
    {
        this._filePath = filePath;
        this._vaultService = vaultService;
    }

    /// <summary>
    /// Gets all queued work requests, migrating the legacy conversion format when detected.
    /// </summary>
    /// <returns>All queued work requests.</returns>
    public IEnumerable<PendingWorkRequest> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this._filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<PendingWorkRequest>();
        }

        if (IsLegacyFormat(json))
        {
            List<LegacyPendingConversionRequest>? legacy = JsonSerializer.Deserialize<List<LegacyPendingConversionRequest>>(json, Options);

            if (legacy == null || legacy.Count == 0)
            {
                return new List<PendingWorkRequest>();
            }

            List<PendingWorkRequest> migrated = legacy
                .Select(l => new PendingWorkRequest(
                    PendingWorkOperations.CurrencyRate,
                    JsonSerializer.Serialize(new { date = l.Date }, Options),
                    l.RequestedAtUtc,
                    status: l.Status,
                    processedAtUtc: l.ProcessedAtUtc,
                    lastError: l.LastError))
                .ToList();

            this.Initialize(migrated);
            return migrated;
        }

        List<PendingWorkRequest>? requests = JsonSerializer.Deserialize<List<PendingWorkRequest>>(json, Options);
        return requests ?? new List<PendingWorkRequest>();
    }

    /// <summary>
    /// Adds a new request or updates an existing request with the same identifier.
    /// </summary>
    /// <param name="request">The request to add or update.</param>
    public void AddOrUpdate(PendingWorkRequest request)
    {
        List<PendingWorkRequest> requests = this.GetAll().ToList();
        requests.RemoveAll(r => r.Id == request.Id);
        requests.Add(request);
        this.Initialize(requests);
    }

    /// <summary>
    /// Replaces all stored requests with the given collection.
    /// </summary>
    /// <param name="requests">The requests to store.</param>
    public void Initialize(IEnumerable<PendingWorkRequest> requests)
    {
        this.EnsureDirectory();
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        string json = JsonSerializer.Serialize(requests, options);
        EncryptedJsonFileStorage.WriteText(this._filePath, json, this._vaultService);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static bool IsLegacyFormat(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return false;
        }

        JsonElement first = document.RootElement[0];
        return first.TryGetProperty("Date", out _) && !first.TryGetProperty("Operation", out _);
    }

    private void EnsureDirectory()
    {
        string? directory = Path.GetDirectoryName(this._filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Shape of the legacy conversion-only queue entries.
    /// </summary>
    private sealed class LegacyPendingConversionRequest
    {
        public DateOnly Date { get; set; }

        public Currencies Source { get; set; }

        public DateTime RequestedAtUtc { get; set; }

        public PendingStatus Status { get; set; }

        public DateTime? ProcessedAtUtc { get; set; }

        public string? LastError { get; set; }
    }
}
namespace MyAccountingApp.Application.Options;

/// <summary>
/// Configuration options for the currency API integration.
/// </summary>
public sealed class CurrencyApiOptions
{
    /// <summary>
    /// Gets or sets the base URL of the currency API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.frankfurter.dev";

    /// <summary>
    /// Gets or sets the name of the provider to use ("Frankfurter" or "ExchangeRateHost").
    /// </summary>
    public string Provider { get; set; } = "Frankfurter";

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the monthly request limit.
    /// </summary>
    public int RequestsLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of requests reserved as a safety margin.
    /// </summary>
    public int SafetyMargin { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of days to backfill on first run when the repository is empty.
    /// </summary>
    public int BackfillDaysOnFirstRun { get; set; } = 90;

    /// <summary>
    /// Gets or sets the maximum number of days a single timeseries request may cover.
    /// </summary>
    public int MaxTimeseriesDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets the name of the external provider.
    /// </summary>
    public string ProviderName { get; set; } = "frankfurter";

    /// <summary>
    /// Gets or sets the list of currencies to exclude from API requests (e.g. "BTC").
    /// </summary>
    public List<string> ExcludeCurrencies { get; set; } = new();
}

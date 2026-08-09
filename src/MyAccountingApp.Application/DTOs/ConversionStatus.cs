namespace MyAccountingApp.Application.DTOs;

/// <summary>
/// Describes the current state of the local conversion store.
/// </summary>
/// <param name="Provider">The name of the provider that supplies the rates.</param>
/// <param name="CachedDays">The number of conversion days currently persisted.</param>
/// <param name="LastCachedDate">The date of the most recently cached conversion, or null if empty.</param>
/// <param name="PendingCount">The number of dates queued for later fetching.</param>
public sealed record ConversionStatus(string Provider, int CachedDays, DateTime? LastCachedDate, int PendingCount);

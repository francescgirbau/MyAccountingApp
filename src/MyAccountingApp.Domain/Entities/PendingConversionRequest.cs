using System;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Entities;

/// <summary>
/// Represents a conversion date that could not be fetched immediately and was queued for later processing.
/// </summary>
public sealed class PendingConversionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PendingConversionRequest"/> class.
    /// </summary>
    /// <param name="date">The date to fetch.</param>
    /// <param name="source">The base currency.</param>
    /// <param name="requestedAtUtc">The UTC timestamp when the request was created.</param>
    /// <param name="status">The initial status of the request.</param>
    /// <param name="processedAtUtc">Optional UTC timestamp when the request was processed.</param>
    /// <param name="lastError">Optional error message from the last processing attempt.</param>
    public PendingConversionRequest(
        DateOnly date,
        Currencies source,
        DateTime requestedAtUtc,
        PendingStatus status = PendingStatus.Pending,
        DateTime? processedAtUtc = null,
        string? lastError = null)
    {
        this.Date = date;
        this.Source = source;
        this.RequestedAtUtc = requestedAtUtc;
        this.Status = status;
        this.ProcessedAtUtc = processedAtUtc;
        this.LastError = lastError;
    }

    /// <summary>
    /// Gets the date to fetch.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Gets the base currency.
    /// </summary>
    public Currencies Source { get; }

    /// <summary>
    /// Gets the UTC timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAtUtc { get; }

    /// <summary>
    /// Gets the current status of the request.
    /// </summary>
    public PendingStatus Status { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the request was processed, if applicable.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Gets the error message from the last processing attempt, if any.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Marks the request as processed.
    /// </summary>
    /// <param name="processedAtUtc">The UTC timestamp of the processing.</param>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        this.Status = PendingStatus.Processed;
        this.ProcessedAtUtc = processedAtUtc;
        this.LastError = null;
    }

    /// <summary>
    /// Marks the request as failed.
    /// </summary>
    /// <param name="error">A description of the failure.</param>
    public void MarkFailed(string error)
    {
        this.Status = PendingStatus.Failed;
        this.LastError = error;
    }
}

using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Entities;

/// <summary>
/// Represents a generic offline work item waiting to be processed by its registered
/// <c>IPendingWorkProcessor</c>. The operation determines who processes it and the
/// payload carries the operation-specific data (JSON).
/// </summary>
public sealed class PendingWorkRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PendingWorkRequest"/> class.
    /// </summary>
    /// <param name="operation">The name of the operation that processes this request.</param>
    /// <param name="payloadJson">The operation-specific payload serialized as JSON.</param>
    /// <param name="requestedAtUtc">The UTC timestamp when the request was created.</param>
    /// <param name="id">Optional identifier; a new one is generated when omitted or empty.</param>
    /// <param name="status">The initial status of the request.</param>
    /// <param name="attempts">The number of processing attempts so far.</param>
    /// <param name="nextRetryAtUtc">Optional UTC timestamp of the next retry, when failed and retryable.</param>
    /// <param name="processedAtUtc">Optional UTC timestamp when the request was processed or last failed.</param>
    /// <param name="lastError">Optional error message from the last processing attempt.</param>
    public PendingWorkRequest(
        string operation,
        string payloadJson,
        DateTime requestedAtUtc,
        Guid id = default,
        PendingStatus status = PendingStatus.Pending,
        int attempts = 0,
        DateTime? nextRetryAtUtc = null,
        DateTime? processedAtUtc = null,
        string? lastError = null)
    {
        this.Id = id == Guid.Empty ? Guid.NewGuid() : id;
        this.Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        this.PayloadJson = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
        this.RequestedAtUtc = requestedAtUtc;
        this.Status = status;
        this.Attempts = attempts;
        this.NextRetryAtUtc = nextRetryAtUtc;
        this.ProcessedAtUtc = processedAtUtc;
        this.LastError = lastError;
    }

    /// <summary>
    /// Gets the unique identifier of the request.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the name of the operation that processes this request.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the operation-specific payload serialized as JSON.
    /// </summary>
    public string PayloadJson { get; }

    /// <summary>
    /// Gets the UTC timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAtUtc { get; }

    /// <summary>
    /// Gets the current status of the request.
    /// </summary>
    public PendingStatus Status { get; private set; }

    /// <summary>
    /// Gets the number of processing attempts so far.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the next retry, or null when the request is done or no longer retryable.
    /// </summary>
    public DateTime? NextRetryAtUtc { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the request was processed or last failed, if applicable.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Gets the error message from the last processing attempt, if any.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Marks the request as currently being processed.
    /// </summary>
    public void MarkProcessing()
    {
        this.Status = PendingStatus.Processing;
    }

    /// <summary>
    /// Marks the request as processed.
    /// </summary>
    /// <param name="processedAtUtc">The UTC timestamp of the processing.</param>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        this.Status = PendingStatus.Processed;
        this.ProcessedAtUtc = processedAtUtc;
        this.LastError = null;
        this.NextRetryAtUtc = null;
    }

    /// <summary>
    /// Marks the request as failed, incrementing the attempt counter and scheduling a retry
    /// unless the maximum number of attempts has been reached.
    /// </summary>
    /// <param name="error">A description of the failure.</param>
    /// <param name="failedAtUtc">The UTC timestamp of the failure.</param>
    /// <param name="retryDelay">Delay until the next retry.</param>
    /// <param name="maxAttempts">Maximum number of processing attempts before giving up.</param>
    public void MarkFailed(string error, DateTime failedAtUtc, TimeSpan retryDelay, int maxAttempts)
    {
        this.Status = PendingStatus.Failed;
        this.LastError = error;
        this.ProcessedAtUtc = failedAtUtc;
        this.Attempts++;
        this.NextRetryAtUtc = this.Attempts < maxAttempts ? failedAtUtc + retryDelay : null;
    }
}
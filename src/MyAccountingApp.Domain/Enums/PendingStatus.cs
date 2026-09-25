namespace MyAccountingApp.Domain.Enums;

/// <summary>
/// Specifies the state of a queued work request.
/// </summary>
public enum PendingStatus
{
    /// <summary>Waiting to be processed.</summary>
    Pending = 0,

    /// <summary>Successfully processed.</summary>
    Processed = 1,

    /// <summary>Processing failed.</summary>
    Failed = 2,

    /// <summary>Currently being processed.</summary>
    Processing = 3,
}

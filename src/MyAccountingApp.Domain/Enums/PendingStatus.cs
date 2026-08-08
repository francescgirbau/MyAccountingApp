namespace MyAccountingApp.Domain.Enums;

/// <summary>
/// Specifies the state of a queued conversion request.
/// </summary>
public enum PendingStatus
{
    /// <summary>Waiting to be processed.</summary>
    Pending = 0,

    /// <summary>Successfully processed.</summary>
    Processed = 1,

    /// <summary>Processing failed.</summary>
    Failed = 2,
}

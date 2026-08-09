namespace MyAccountingApp.Application.DTOs;

/// <summary>
/// Summarizes the result of processing the pending conversion queue.
/// </summary>
public sealed record PendingProcessingResult(int ProcessedDays, int RequestsSpent, int Failures);

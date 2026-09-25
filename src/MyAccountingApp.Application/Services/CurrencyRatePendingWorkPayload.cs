using System.Text.Json;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Payload of a <c>CurrencyRate</c> pending work request: the date whose conversion
/// needs to be fetched when API quota becomes available.
/// </summary>
/// <param name="Date">The date to fetch.</param>
public sealed record CurrencyRatePendingWorkPayload(DateOnly Date)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Serializes the payload for a date.
    /// </summary>
    /// <param name="date">The date to fetch.</param>
    /// <returns>The serialized payload JSON.</returns>
    public static string Create(DateOnly date)
    {
        return JsonSerializer.Serialize(new CurrencyRatePendingWorkPayload(date), Options);
    }

    /// <summary>
    /// Reads the payload date from a pending work request.
    /// </summary>
    /// <param name="request">The pending work request.</param>
    /// <returns>The date to fetch.</returns>
    public static DateOnly ReadDate(PendingWorkRequest request)
    {
        CurrencyRatePendingWorkPayload payload = JsonSerializer.Deserialize<CurrencyRatePendingWorkPayload>(request.PayloadJson, Options)
            ?? throw new InvalidOperationException($"Pending work request {request.Id} has an invalid CurrencyRate payload.");

        return payload.Date;
    }
}
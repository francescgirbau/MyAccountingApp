using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;

namespace MyAccountingApp.Api.Endpoints;

/// <summary>
/// Maps the self-hosted Frankfurter-compatible endpoint served from the local conversion cache.
/// </summary>
public static class FrankfurterEndpoints
{
    /// <summary>
    /// Maps the internal Frankfurter-compatible rates endpoint under the API prefix.
    /// </summary>
    /// <param name="app">The web application to map the endpoints on.</param>
    public static void MapFrankfurterEndpoints(this WebApplication app)
    {
        app.MapGet($"{ApiEndpoints.ApiPrefix}/frankfurter/v2/rates", (
            FrankfurterSelfHostService service,
            DateTime? date,
            DateTime? from,
            DateTime? to,
            string? @base,
            string? quotes) =>
        {
            Currencies source = Currencies.EUR;

            if (@base is not null && !Enum.TryParse<Currencies>(@base, true, out source))
            {
                return Results.BadRequest(new { message = $"Unknown base currency '{@base}'" });
            }

            if (date.HasValue && (from.HasValue || to.HasValue))
            {
                return Results.BadRequest(new { message = "Use either 'date' or 'from' with 'to', not both" });
            }

            IReadOnlyCollection<Currencies>? targets = null;

            if (quotes is not null)
            {
                List<Currencies> parsed = new();

                foreach (string code in quotes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Enum.TryParse<Currencies>(code, true, out Currencies currency))
                    {
                        return Results.BadRequest(new { message = $"Unknown quote currency '{code}'" });
                    }

                    parsed.Add(currency);
                }

                targets = parsed;
            }

            try
            {
                IReadOnlyList<FrankfurterRateRecord> records;

                if (date.HasValue)
                {
                    records = service.GetRatesAsync(DateOnly.FromDateTime(date.Value), source, targets);
                }
                else
                {
                    DateOnly start = DateOnly.FromDateTime((from ?? DateTime.UtcNow.AddDays(-7)).Date);
                    DateOnly end = DateOnly.FromDateTime((to ?? DateTime.UtcNow.Date).Date);

                    if (end < start)
                    {
                        return Results.BadRequest(new { message = "'to' must be greater than or equal to 'from'" });
                    }

                    records = service.GetRatesAsync(start, end, source, targets);
                }

                return Results.Ok(records);
            }
            catch (ConversionNotAvailableException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });
    }
}
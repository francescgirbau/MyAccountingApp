namespace MyAccountingApp.Application.Services;

/// <summary>
/// Computes the client-side deep links that annotate validation issues.
/// </summary>
internal static class ValidationDeepLink
{
    public static string? Build(string entityType, string? symbol, IReadOnlyList<Guid>? entityIds)
    {
        if (entityIds is { Count: > 0 })
        {
            return $"/{EntityPath(entityType)}?ids={string.Join(",", entityIds)}";
        }

        if (symbol is not null)
        {
            return $"/asset-transactions?symbol={Uri.EscapeDataString(symbol)}";
        }

        return null;
    }

    private static string EntityPath(string entityType) => entityType switch
    {
        "AssetTransaction" => "asset-transactions",
        "OptionTransaction" => "option-transactions",
        _ => "transactions",
    };
}
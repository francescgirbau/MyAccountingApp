namespace MyAccountingApp.Web.Components;

/// <summary>Pure display-formatting helpers shared by the Portfolio page components.</summary>
public static class PortfolioDisplay
{
    // A soft amber tint over the current surface: visible in both light and dark themes,
    // unlike the old #fff9c4 which was unreadable on a dark background.
    private const string HighlightedRow = "background-color: color-mix(in srgb, var(--mud-palette-warning) 18%, var(--mud-palette-surface));";

    public static string FormatAsOf(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string? HighlightStyle(string? highlightedSymbol, string symbol) =>
        highlightedSymbol == symbol ? HighlightedRow : null;
}

namespace MyAccountingApp.Web.Components;

/// <summary>Pure display-formatting helpers shared by the Portfolio page components.</summary>
public static class PortfolioDisplay
{
    private const string HighlightedRow = "background-color: #fff9c4;";

    public static string FormatAsOf(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string? HighlightStyle(string? highlightedSymbol, string symbol) =>
        highlightedSymbol == symbol ? HighlightedRow : null;
}

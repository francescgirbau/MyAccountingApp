namespace MyAccountingApp.Web.Components;

/// <summary>One line of the allocation legend, joining the current and purchase allocations of a symbol.</summary>
public sealed record PortfolioLegendItem(
    string Key,
    decimal ValueEur,
    decimal CurrentWeight,
    decimal? PurchaseWeight,
    decimal? PnLPct,
    string Color)
{
    public decimal WeightDelta => this.CurrentWeight - (this.PurchaseWeight ?? 0);
    public bool IsNavigable => this.Key != "Other";
}

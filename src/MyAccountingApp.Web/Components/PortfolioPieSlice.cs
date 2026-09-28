namespace MyAccountingApp.Web.Components;

/// <summary>SVG path of a single donut-chart slice, plus the values shown in the allocation legend.</summary>
public sealed record PortfolioPieSlice(string Key, string Path, string Color, decimal ValueEur, decimal Weight)
{
    public bool IsNavigable => this.Key != "Other";
}

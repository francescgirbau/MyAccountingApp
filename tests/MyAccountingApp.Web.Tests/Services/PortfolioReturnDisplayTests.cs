using MyAccountingApp.Contracts;
using MyAccountingApp.Web.Services;

namespace MyAccountingApp.Web.Tests.Services;

public class PortfolioReturnDisplayTests
{
    [Fact]
    public void MainRate_PrefersAnnualized_WhenAvailable()
    {
        Assert.Equal(0.2255m, PortfolioReturnDisplay.MainRate(Annualized()));
    }

    [Fact]
    public void MainRate_FallsBackToTotal_WhenNotAnnualized()
    {
        Assert.Equal(0.10m, PortfolioReturnDisplay.MainRate(NotAnnualized()));
    }

    [Fact]
    public void MainRate_ReturnsNull_WhenResultUnavailable()
    {
        Assert.Null(PortfolioReturnDisplay.MainRate(null));
    }

    [Fact]
    public void IsAnnualized_ReflectsAnnualizedReturn()
    {
        Assert.True(PortfolioReturnDisplay.IsAnnualized(Annualized()));
        Assert.False(PortfolioReturnDisplay.IsAnnualized(NotAnnualized()));
        Assert.False(PortfolioReturnDisplay.IsAnnualized(null));
    }

    [Fact]
    public void Tooltip_ExplainsNoFlows_WhenResultUnavailable()
    {
        Assert.Contains("no investment flows", PortfolioReturnDisplay.Tooltip(null));
    }

    [Fact]
    public void Tooltip_StatesAnnualized_WithSupportingFigures()
    {
        string tooltip = PortfolioReturnDisplay.Tooltip(Annualized());

        Assert.Contains("annualized", tooltip);
        Assert.Contains("Capital invested", tooltip);
        Assert.Contains("average holding period", tooltip);
        Assert.Contains("€", tooltip);
    }

    [Fact]
    public void Tooltip_StatesNotAnnualized_AndMentionsExcludedFlows()
    {
        string tooltip = PortfolioReturnDisplay.Tooltip(NotAnnualized());

        Assert.Contains("not annualized", tooltip);
        Assert.Contains("below one year", tooltip);
        Assert.Contains("1 cash flow", tooltip);
    }

    private static PortfolioReturnDto Annualized() => new(
        TotalReturn: 0.50m,
        AnnualizedReturn: 0.2255m,
        AyiYears: 2m,
        CapitalInvestedEur: 1000m,
        TotalProceedsEur: 100m,
        TerminalValueEur: 1500m,
        ExcludedFlowCount: 0);

    private static PortfolioReturnDto NotAnnualized() => new(
        TotalReturn: 0.10m,
        AnnualizedReturn: null,
        AyiYears: 0.6m,
        CapitalInvestedEur: 1000m,
        TotalProceedsEur: 0m,
        TerminalValueEur: 1100m,
        ExcludedFlowCount: 1);
}
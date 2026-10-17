using MyAccountingApp.Contracts;
using MyAccountingApp.Web.Services;

namespace MyAccountingApp.Web.Tests.Services;

public class InvestingPositionsCsvTests
{
    [Fact]
    public void Build_HeaderMatchesInvestingFormat()
    {
        string csv = InvestingPositionsCsv.Build(new List<PortfolioPositionDto>());

        Assert.Equal("Symbol,Open Price,Amount,Type,Currency" + Environment.NewLine, csv);
    }

    [Fact]
    public void Build_LongAndShortPositions_UseAbsoluteAmountsAndDirection()
    {
        List<PortfolioPositionDto> positions = new()
        {
            Position("ADSd", 14m, 145.6786m, "EUR"),
            Position("SEPLl", -100m, 2.555m, "GBP"),
        };

        string csv = InvestingPositionsCsv.Build(positions);

        string expected =
            "Symbol,Open Price,Amount,Type,Currency" + Environment.NewLine +
            "ADSd,145.6786,14,BUY,EUR" + Environment.NewLine +
            "SEPLl,2.555,100,SELL,GBP" + Environment.NewLine;
        Assert.Equal(expected, csv);
    }

    [Fact]
    public void Build_SkipsClosedPositions()
    {
        List<PortfolioPositionDto> positions = new()
        {
            Position("NEM", 0m, 0m, "USD"),
            Position("GOLD", 200m, 16.25m, "USD"),
        };

        string csv = InvestingPositionsCsv.Build(positions);

        Assert.DoesNotContain("NEM", csv);
        Assert.Contains("GOLD,16.25,200,BUY,USD", csv);
    }

    [Fact]
    public void Build_FormatsNumbersWithInvariantCulture()
    {
        List<PortfolioPositionDto> positions = new()
        {
            Position("ALNTe", 200m, 10.123456m, "EUR"),
        };

        string csv = InvestingPositionsCsv.Build(positions);

        Assert.Contains("ALNTe,10.1235,200,BUY,EUR", csv);
        Assert.DoesNotContain("10,1235", csv);
    }

    [Fact]
    public void Build_QuotesSymbolsContainingComma()
    {
        List<PortfolioPositionDto> positions = new()
        {
            Position("A,B", 10m, 1.5m, "USD"),
        };

        string csv = InvestingPositionsCsv.Build(positions);

        Assert.Contains("\"A,B\",1.5,10,BUY,USD", csv);
    }

    private static PortfolioPositionDto Position(string symbol, decimal netQuantity, decimal averageUnitaryCost, string currency) =>
        new(
            Symbol: symbol,
            NetQuantity: netQuantity,
            AverageUnitaryCost: averageUnitaryCost,
            TotalCostBasis: netQuantity * averageUnitaryCost,
            Currency: currency,
            TransactionCount: 1,
            RealizedGainLoss: 0m,
            OpenLots: new List<TaxLotDto>(),
            MarketPrice: null,
            UnrealizedGainLoss: null,
            HasShortfall: false,
            UnmatchedSellQuantity: 0m);
}

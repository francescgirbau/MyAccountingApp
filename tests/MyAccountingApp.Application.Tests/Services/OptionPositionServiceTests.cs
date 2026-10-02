namespace MyAccountingApp.Application.Tests.Services;

using MyAccountingApp.Application.Services;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

public class OptionPositionServiceTests
{
    private static OptionTransaction Leg(DateTime date, string description, string symbol, decimal premium, int contracts, AssetTransactionType type, string currency = "EUR")
    {
        TransactionCategory category = type == AssetTransactionType.Buy ? TransactionCategory.INVESTMENT : TransactionCategory.DIVESTMENT;
        Transaction transaction = new(date, description, new Money(premium, currency), category);
        return new OptionTransaction(transaction, symbol, string.Empty, contracts, type);
    }

    private static OptionPositionService CreateService(params OptionTransaction[] legs)
    {
        FakeOptionRepository repo = new();
        foreach (OptionTransaction leg in legs)
        {
            repo.Add(leg);
        }

        return new OptionPositionService(repo);
    }

    [Fact]
    public void GetPositions_ClosedShortPut_ComputesPositionAndReturns()
    {
        // Arrange: short put sold at 3.00 (300 total) and closed by assignment at zero premium.
        OptionPositionService service = CreateService(
            Leg(new DateTime(2024, 5, 22), "DGE 21JUN24 27 P", "DGE", 300, 1, AssetTransactionType.Sell, "GBP"),
            Leg(new DateTime(2024, 6, 21), "DGE 21JUN24 27 P", "DGE", 0, 1, AssetTransactionType.Buy, "GBP"));

        // Act
        OptionPositionDto position = Assert.Single(service.GetPositions());

        // Assert
        Assert.Equal("DGE 21JUN24 27 P", position.Key);
        Assert.Equal("DGE 21JUN24 27 P", position.Symbol);
        Assert.Equal("DGE", position.Underlying);
        Assert.Equal("Short Put", position.Strategy);
        Assert.Equal("Put", position.Side);
        Assert.Equal("Sell", position.Direction);
        Assert.Equal(new DateOnly(2024, 6, 21), position.Expiration);
        Assert.Equal(27m, position.Strike);
        Assert.Equal(0m, position.Quantity);
        Assert.Equal("GBP", position.Currency);
        Assert.Equal(new DateOnly(2024, 5, 22), position.OpenDate);
        Assert.Equal(new DateOnly(2024, 6, 21), position.CloseDate);
        Assert.Equal("Closed", position.Status);
        Assert.Equal(300m, position.Credit);
        Assert.Equal(0m, position.Debit);
        Assert.Equal(300m, position.ProfitLoss);
        Assert.Equal(2400m, position.CapitalAtRisk);
        Assert.Equal(0.1250m, position.Yield);
        Assert.Equal(30, position.DaysHeld);
        Assert.Equal(1.5208m, position.AnnualizedReturn);
    }

    [Fact]
    public void GetPositions_OpenLongCall_TracksDebitAndStaysOpen()
    {
        // Arrange
        DateTime open = new DateTime(2025, 1, 2);
        OptionPositionService service = CreateService(
            Leg(open, "VET 16JAN26 10 C", "VET", 110.11m, 2, AssetTransactionType.Buy));

        // Act
        OptionPositionDto position = Assert.Single(service.GetPositions());

        // Assert
        Assert.Equal("Long Call", position.Strategy);
        Assert.Equal("VET 16JAN26 10 C", position.Key);
        Assert.Equal(new DateOnly(2026, 1, 16), position.Expiration);
        Assert.Equal(10m, position.Strike);
        Assert.Equal(2m, position.Quantity);
        Assert.Equal("Open", position.Status);
        Assert.Null(position.CloseDate);
        Assert.Equal(0m, position.Credit);
        Assert.Equal(110.11m, position.Debit);
        Assert.Equal(110.11m, position.CapitalAtRisk);
        Assert.Equal(-110.11m, position.ProfitLoss);
        Assert.Equal(-1.0m, position.Yield);
        int expectedDays = DateOnly.FromDateTime(DateTime.Today).DayNumber - new DateOnly(2025, 1, 2).DayNumber;
        Assert.Equal(expectedDays, position.DaysHeld);
    }

    [Fact]
    public void GetPositions_OpenShortPut_KeepsNegativeQuantityAndCapitalAtRisk()
    {
        // Arrange
        OptionPositionService service = CreateService(
            Leg(new DateTime(2025, 3, 10), "KHC 18APR25 45 P", "KHC", 40, 1, AssetTransactionType.Sell, "USD"));

        // Act
        OptionPositionDto position = Assert.Single(service.GetPositions());

        // Assert
        Assert.Equal("Short Put", position.Strategy);
        Assert.Equal(-1m, position.Quantity);
        Assert.Equal("Open", position.Status);
        Assert.Null(position.CloseDate);
        Assert.Equal(40m, position.Credit);
        Assert.Equal(0m, position.Debit);
        Assert.Equal(40m, position.ProfitLoss);
        Assert.Equal(4460m, position.CapitalAtRisk);
        Assert.Equal(0.0090m, position.Yield);
    }

    [Fact]
    public void GetPositions_ManualEntriesWithoutContract_GroupBySymbol()
    {
        // Arrange: manual legs whose description carries no contract (strike/expiry/side).
        OptionPositionService service = CreateService(
            Leg(new DateTime(2024, 1, 10), "VET", "VET", 50, 1, AssetTransactionType.Sell),
            Leg(new DateTime(2024, 3, 10), "VET", "VET", 0, 1, AssetTransactionType.Buy));

        // Act
        OptionPositionDto position = Assert.Single(service.GetPositions());

        // Assert
        Assert.Equal("VET", position.Key);
        Assert.Equal("VET", position.Symbol);
        Assert.Null(position.Strike);
        Assert.Null(position.Expiration);
        Assert.Equal("?", position.Side);
        Assert.Equal("Short", position.Strategy);
        Assert.Equal("Closed", position.Status);
        Assert.Equal(50m, position.Credit);
        Assert.Equal(0m, position.Debit);
    }

    [Fact]
    public void GetPositions_DifferentContractsOfSameRoot_AreNotMerged()
    {
        // Arrange
        OptionPositionService service = CreateService(
            Leg(new DateTime(2025, 1, 2), "VET 16JAN26 10 C", "VET", 110.11m, 2, AssetTransactionType.Buy),
            Leg(new DateTime(2025, 1, 3), "VET 16JAN27 15 P", "VET", 200, 1, AssetTransactionType.Sell));

        // Act
        List<OptionPositionDto> positions = service.GetPositions().ToList();

        // Assert
        Assert.Equal(2, positions.Count);
        Assert.Contains(positions, p => p.Key == "VET 16JAN26 10 C" && p.Strategy == "Long Call");
        Assert.Contains(positions, p => p.Key == "VET 16JAN27 15 P" && p.Strategy == "Short Put");
    }

    [Fact]
    public void GetPositions_DecimalStrikeAndFourDigitYear_AreParsed()
    {
        // Arrange
        OptionPositionService service = CreateService(
            Leg(new DateTime(2025, 8, 1), "DGE 15AUG25 19.5 C", "DGE", 195, 1, AssetTransactionType.Sell, "GBP"),
            Leg(new DateTime(2025, 11, 1), "SPX 21DEC2025 5000 C", "SPX", 1200, 1, AssetTransactionType.Buy, "USD"));

        // Act
        List<OptionPositionDto> positions = service.GetPositions().ToList();

        // Assert
        OptionPositionDto dge = positions.Single(p => p.Key == "DGE 15AUG25 19.5 C");
        Assert.Equal(19.5m, dge.Strike);
        Assert.Equal(new DateOnly(2025, 8, 15), dge.Expiration);
        Assert.Equal("Call", dge.Side);

        OptionPositionDto spx = positions.Single(p => p.Underlying == "SPX");
        Assert.Equal(5000m, spx.Strike);
        Assert.Equal(new DateOnly(2025, 12, 21), spx.Expiration);
    }

    [Fact]
    public void GetPositions_OrdersByOpenDateNewestFirst()
    {
        // Arrange
        OptionPositionService service = CreateService(
            Leg(new DateTime(2024, 1, 2), "DGE 21JUN24 27 P", "DGE", 300, 1, AssetTransactionType.Sell, "GBP"),
            Leg(new DateTime(2025, 1, 2), "VET 16JAN26 10 C", "VET", 110.11m, 2, AssetTransactionType.Buy));

        // Act
        List<OptionPositionDto> positions = service.GetPositions().ToList();

        // Assert
        Assert.Equal(2, positions.Count);
        Assert.Equal("VET 16JAN26 10 C", positions[0].Key);
        Assert.Equal("DGE 21JUN24 27 P", positions[1].Key);
    }
}
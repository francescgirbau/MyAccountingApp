namespace MyAccountingApp.Application.Tests.Services;
using System;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

public class AssetTransactionDisplayTests
{
    [Fact]
    public void GetCategoryLabel_BuyInvestment_ReturnsAssetPurchase()
    {
        // AssetTransaction Buy is shown as a human "Asset purchase" while keeping the technical category.
        string label = AssetTransactionDisplay.GetCategoryLabel("Buy", "INVESTMENT");

        Assert.Equal("Asset purchase", label);
    }

    [Fact]
    public void GetCategoryTooltip_BuyInvestment_ClarifiesItIsNotAnOperatingExpense()
    {
        string tooltip = AssetTransactionDisplay.GetCategoryTooltip("Buy", "INVESTMENT");

        Assert.Contains("not an operating expense", tooltip);
    }

    [Fact]
    public void GetCategoryLabel_SellDivestment_ReturnsAssetSale()
    {
        // AssetTransaction Sell is shown as a human "Asset sale" while keeping the technical category.
        string label = AssetTransactionDisplay.GetCategoryLabel("Sell", "DIVESTMENT");

        Assert.Equal("Asset sale", label);
    }

    [Fact]
    public void GetCategoryTooltip_SellDivestment_ClarifiesItIsNotOperatingIncome()
    {
        string tooltip = AssetTransactionDisplay.GetCategoryTooltip("Sell", "DIVESTMENT");

        Assert.Contains("not operating income", tooltip);
    }

    [Fact]
    public void GetCategoryLabel_UnrecognizedCombination_FallsBackToTechnicalCategory()
    {
        Assert.Equal("INVESTMENT", AssetTransactionDisplay.GetCategoryLabel("Sell", "INVESTMENT"));
        Assert.Equal("CORPORATE_ACTION", AssetTransactionDisplay.GetCategoryLabel("CorporateAction", "CORPORATE_ACTION"));
        Assert.Empty(AssetTransactionDisplay.GetCategoryTooltip("CorporateAction", "CORPORATE_ACTION"));
    }

    [Theory]
    [InlineData(2024, true, "/asset-transactions?year=2024&type=Buy")]
    [InlineData(2023, false, "/asset-transactions?year=2023&type=Sell")]
    [InlineData(2020, true, "/asset-transactions?year=2020&type=Buy")]
    public void BuildDeepLink_EncodesYearAndType(int year, bool purchase, string expected)
    {
        Assert.Equal(expected, AssetTransactionDisplay.BuildDeepLink(year, purchase));
    }

    [Fact]
    public void BuildInvestmentsLink_NoYear_NavigatesToAssetTransactions()
    {
        Assert.Equal("/asset-transactions", AssetTransactionDisplay.BuildInvestmentsLink(year: null));
    }

    [Theory]
    [InlineData(2024, "/asset-transactions?year=2024")]
    [InlineData(2022, "/asset-transactions?year=2022")]
    public void BuildInvestmentsLink_SelectedYear_NavigatesWithYearFilter(int year, string expected)
    {
        Assert.Equal(expected, AssetTransactionDisplay.BuildInvestmentsLink(year));
    }

    [Theory]
    [InlineData(2024, "/asset-transactions?year=2024")]
    [InlineData(2021, "/asset-transactions?year=2021")]
    public void BuildDeepLink_YearOnly_OmitsTypeFilter(int year, string expected)
    {
        Assert.Equal(expected, AssetTransactionDisplay.BuildDeepLink(year));
    }

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    public void MatchesFilter_AppliesCorrectYearAndType(int year, bool purchase)
    {
        AssetTransactionType type = purchase
            ? AssetTransactionType.Buy
            : AssetTransactionType.Sell;

        AssetTransaction buy2024 = Tx(2024, AssetTransactionType.Buy);
        AssetTransaction sell2024 = Tx(2024, AssetTransactionType.Sell);
        AssetTransaction buy2023 = Tx(2023, AssetTransactionType.Buy);
        AssetTransaction sell2023 = Tx(2023, AssetTransactionType.Sell);

        Assert.True(AssetTransactionDisplay.MatchesFilter(purchase ? buy2024 : sell2023, year, purchase));
        Assert.False(AssetTransactionDisplay.MatchesFilter(buy2023, 2024, purchase: true));
        Assert.False(AssetTransactionDisplay.MatchesFilter(sell2024, 2024, purchase: true));
        Assert.False(AssetTransactionDisplay.MatchesFilter(buy2024, 2024, purchase: false));
    }

    private static AssetTransaction Tx(int year, AssetTransactionType type) =>
        new(new Transaction(Guid.NewGuid(), new DateTime(year, 6, 1), "Test", new Money(100, "EUR"), TransactionCategory.INCOME),
            "TEST",
            5,
            type);
}

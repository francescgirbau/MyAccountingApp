using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.Services;
using Xunit;

namespace MyAccountingApp.Application.Tests.Services;

public class TransactionCategoryFilterTests
{
    [Fact]
    public void FilterByCategories_NoCategories_ReturnsAll()
    {
        List<Item> all = Items("EXPENSE", "INCOME", "TRANSFER", "DEPOSIT");
        HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<Item> result = TransactionCategoryFilter.FilterByCategories(all, categories, i => i.Category).ToList();

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void FilterByCategories_OnlyTransfer_ReturnsOnlyTransfers()
    {
        List<Item> all = Items("EXPENSE", "TRANSFER", "DEPOSIT", "TRANSFER");

        List<Item> result = Filter(all, "TRANSFER");

        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("TRANSFER", i.Category));
    }

    [Fact]
    public void FilterByCategories_OnlyDeposit_ReturnsOnlyDeposits()
    {
        List<Item> all = Items("EXPENSE", "TRANSFER", "DEPOSIT");

        List<Item> result = Filter(all, "DEPOSIT");

        Assert.Single(result);
        Assert.Equal("DEPOSIT", result[0].Category);
    }

    [Fact]
    public void FilterByCategories_DepositAndTransfer_ReturnsBoth()
    {
        List<Item> all = Items("EXPENSE", "INCOME", "TRANSFER", "DEPOSIT", "TRANSFER");

        List<Item> result = Filter(all, "DEPOSIT", "TRANSFER");

        Assert.Equal(3, result.Count);
        Assert.All(result, i => Assert.True(i.Category is "DEPOSIT" or "TRANSFER"));
    }

    [Fact]
    public void FilterByCategories_DepositAndTransfer_ExcludesOtherCategories()
    {
        List<Item> all = Items("EXPENSE", "INCOME", "FEE", "DIVIDEND", "TRANSFER", "DEPOSIT", "WITHHOLDING_TAX", "INTEREST", "INVESTMENT");

        List<Item> result = Filter(all, "DEPOSIT", "TRANSFER");

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, i => i.Category is "EXPENSE" or "INCOME" or "FEE" or "DIVIDEND" or "WITHHOLDING_TAX" or "INTEREST" or "INVESTMENT");
    }

    [Fact]
    public void FilterByCategories_IsCaseInsensitive()
    {
        List<Item> all = Items("EXPENSE", "TRANSFER");

        List<Item> result = Filter(all, "transfer", "deposit");

        Assert.Single(result);
        Assert.Equal("TRANSFER", result[0].Category);
    }

    [Fact]
    public void FilterByCategories_UnknownCategory_ReturnsNothing()
    {
        List<Item> all = Items("EXPENSE", "TRANSFER");

        HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REVOLUT" };

        List<Item> result = TransactionCategoryFilter.FilterByCategories(all, categories, i => i.Category).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void FilterByCategories_EmptyInput_ReturnsNothing()
    {
        List<Item> result = Filter(new List<Item>(), "TRANSFER");

        Assert.Empty(result);
    }

    private static List<Item> Filter(List<Item> items, params string[] categories)
    {
        HashSet<string> set = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
        return TransactionCategoryFilter.FilterByCategories(items, set, i => i.Category).ToList();
    }

    private static List<Item> Items(params string[] categories) => categories.Select(c => new Item(c)).ToList();

    private sealed class Item
    {
        public Item(string category)
        {
            this.Category = category;
        }

        public string Category { get; }
    }
}
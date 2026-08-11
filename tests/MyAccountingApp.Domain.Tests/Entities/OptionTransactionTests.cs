using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Tests.Entities;

public class OptionTransactionTests
{
    [Fact]
    public void UpdateSymbol_ShouldChangeSymbol_WhenValid()
    {
        DateTime date = new(2025, 8, 27);
        Money money = new(100, "EUR");
        Transaction transaction = new(date, "Option", money, TransactionCategory.EXPENSE);
        OptionTransaction option = new(transaction, "AAPL", "US0378331005", 2, AssetTransactionType.Buy);

        option.UpdateSymbol("AAPL 260918C00210000");

        Assert.Equal("AAPL 260918C00210000", option.Symbol);
    }

    [Fact]
    public void UpdateSymbol_ShouldThrow_WhenSymbolIsEmpty()
    {
        DateTime date = new(2025, 8, 27);
        Money money = new(100, "EUR");
        Transaction transaction = new(date, "Option", money, TransactionCategory.EXPENSE);
        OptionTransaction option = new(transaction, "AAPL", "US0378331005", 2, AssetTransactionType.Buy);

        Assert.Throws<ArgumentException>(() => option.UpdateSymbol(string.Empty));
    }
}
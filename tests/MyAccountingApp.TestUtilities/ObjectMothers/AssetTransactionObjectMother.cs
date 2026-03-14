using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.TestUtilities.ObjectMothers;

public static class AssetTransactionObjectMother
{
    public static AssetTransaction CreateSell(
        string symbol = "AAPL",
        double price = 150,
        double quantity = 10,
        string currency = "USD")
    {
        Transaction transaction = new Transaction(
            date: DateTime.UtcNow,
            description: "Sell transaction",
            money: new Money(price * quantity, currency),
            category: TransactionCategory.INCOME
        );

        return new AssetTransaction(transaction, symbol, quantity, AssetTransactionType.Sell);
    }

    public static AssetTransaction CreateBuy(
        string symbol = "AAPL",
        double price = 150,
        double quantity = 10,
        string currency = "USD")
    {
        Transaction transaction = new Transaction(
            date: DateTime.UtcNow,
            description: "Buy transaction",
            money: new Money(-price * quantity, currency),
            category: TransactionCategory.EXPENSE
        );

        return new AssetTransaction(transaction, symbol, quantity, AssetTransactionType.Buy);
    }
}

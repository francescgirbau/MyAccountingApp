using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.TestUtilities.ObjectMothers;
public static class TransactionObjectMother
{
    public static Transaction ValidIncome(
        double amount = 100,
        Currencies currency = Currencies.EUR,
        string description = "Test Income")
    {
        return new Transaction(
            date: DateTime.UtcNow,
            description: description,
            money: new Money() { Amount = amount, Currency = currency },
            category: TransactionCategory.INCOME);
    }

    public static Transaction ValidExpense(
        double amount = -50,
        Currencies currency = Currencies.EUR,
        string description = "Test Expense")
    {
        return new Transaction(
            date: DateTime.UtcNow,
            description: description,
            money: new Money() { Amount = amount, Currency = currency },
            category: TransactionCategory.EXPENSE);
    }

    public static Transaction ValidTransfer(
        double amount = 200,
        Currencies currency = Currencies.EUR,
        string description = "Test Transfer")
    {
        return new Transaction(
            date: DateTime.UtcNow,
            description: description,
            money: new Money() { Amount = amount, Currency = currency },
            category: TransactionCategory.TRANSFER);
    }
}

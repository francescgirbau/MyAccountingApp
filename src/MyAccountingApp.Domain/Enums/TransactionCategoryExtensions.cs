namespace MyAccountingApp.Domain.Enums;

public static class TransactionCategoryExtensions
{
    public static bool IsCashIncome(this TransactionCategory category) =>
        category is TransactionCategory.INCOME or TransactionCategory.DIVIDEND or TransactionCategory.INTEREST;

    public static bool IsCashExpense(this TransactionCategory category) =>
        category is TransactionCategory.EXPENSE or TransactionCategory.FEE or TransactionCategory.WITHHOLDING_TAX;
}

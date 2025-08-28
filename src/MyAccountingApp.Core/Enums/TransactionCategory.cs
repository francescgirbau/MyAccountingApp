namespace MyAccountingApp.Core.Enums;

/// <summary>
/// Specifies the category of a financial transaction.
/// </summary>
public enum TransactionCategory
{
    /// <summary>
    /// An expense transaction.
    /// </summary>
    EXPENSE = 0,

    /// <summary>
    /// An income transaction.
    /// </summary>
    INCOME = 1,

    /// <summary>
    /// A transfer transaction.
    /// </summary>
    TRANSFER = 2,
}
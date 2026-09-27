namespace MyAccountingApp.Web.Components;

/// <summary>Cash-flow category buckets used by the quick filters of the Transactions page.</summary>
public static class TransactionBuckets
{
    public static readonly string[] Operating = { "INCOME", "EXPENSE", "DIVIDEND", "INTEREST", "FEE", "WITHHOLDING_TAX" };
    public static readonly string[] Internal = { "TRANSFER", "DEPOSIT" };
    public static readonly string[] Loans = { "LOAN_IN", "LOAN_OUT" };
    public static readonly string[] Fx = { "FX_CONVERSION" };

    public static bool IsBucketActive(IEnumerable<string> bucket, IReadOnlySet<string> selected) => bucket.All(selected.Contains);
}
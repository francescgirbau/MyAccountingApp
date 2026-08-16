namespace MyAccountingApp.Domain.Enums;

/// <summary>
/// Identifies which side of an FX conversion pair a transaction is.
/// </summary>
public enum FxLeg
{
    /// <summary>
    /// Cash leaves the account (the currency being sold).
    /// </summary>
    Out = 0,

    /// <summary>
    /// Cash enters the account (the currency being bought).
    /// </summary>
    In = 1,
}
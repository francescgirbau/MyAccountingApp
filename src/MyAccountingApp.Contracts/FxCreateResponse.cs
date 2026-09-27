namespace MyAccountingApp.Contracts;

public sealed record FxCreateResponse(
    Guid PairId,
    TransactionDto OutTransaction,
    TransactionDto InTransaction);
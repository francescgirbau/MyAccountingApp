namespace MyAccountingApp.Contracts;

public sealed record LoanCashFlowDto(
    decimal Disbursed,
    decimal Repaid,
    decimal NetOutstanding);
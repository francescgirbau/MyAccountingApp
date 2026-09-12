namespace MyAccountingApp.Application.DTOs;

public sealed record LoanCashFlowDto(
    decimal Disbursed,
    decimal Repaid,
    decimal NetOutstanding);
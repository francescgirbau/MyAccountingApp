using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface ILoanCommandService
{
    LoanSummaryDto CreateLoan(CreateLoanRequest request);

    LoanSummaryDto AddRepayment(Guid loanId, AddLoanRepaymentRequest request);

    bool CloseLoan(Guid loanId);

    bool DeleteLoan(Guid loanId);
}
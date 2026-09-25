using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface ILoanCommandService
{
    LoanSummaryDto CreateLoan(CreateLoanRequest request);

    LoanSummaryDto AddRepayment(Guid loanId, AddLoanRepaymentRequest request);

    LoanSummaryDto UpdateMovement(Guid loanId, Guid movementId, UpdateLoanMovementRequest request);

    bool DeleteMovement(Guid loanId, Guid movementId);

    bool CloseLoan(Guid loanId);

    bool DeleteLoan(Guid loanId);
}
using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface ILoanQuery
{
    List<LoanSummaryDto> GetAll();

    LoanSummaryDto? GetById(Guid loanId);
}
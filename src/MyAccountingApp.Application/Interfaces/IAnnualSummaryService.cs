using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface IAnnualSummaryService
{
    List<AnnualSummaryDto> GetAll();
    AnnualSummaryDto? GetByYear(int year);
}

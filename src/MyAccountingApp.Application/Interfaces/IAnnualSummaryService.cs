using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IAnnualSummaryService
{
    List<AnnualSummaryDto> GetAll();
    AnnualSummaryDto? GetByYear(int year);
}

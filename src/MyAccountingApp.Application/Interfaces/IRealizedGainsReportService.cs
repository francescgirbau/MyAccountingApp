using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IRealizedGainsReportService
{
    Task<RealizedGainsReportDto> GetRealizedGainsAsync(int year);

    Task<WithholdingReportDto> GetWithholdingAsync(int year);
}

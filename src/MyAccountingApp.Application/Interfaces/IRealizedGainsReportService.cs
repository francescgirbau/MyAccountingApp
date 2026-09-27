using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface IRealizedGainsReportService
{
    Task<RealizedGainsReportDto> GetRealizedGainsAsync(int year);

    Task<WithholdingReportDto> GetWithholdingAsync(int year);
}

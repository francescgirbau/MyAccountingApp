using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IDashboardQuery
{
    Task<DashboardDto> GetAsync(DateOnly asOf);
}
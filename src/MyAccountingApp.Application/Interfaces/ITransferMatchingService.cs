using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface ITransferMatchingService
{
    TransferMatchingResult Recalculate();
}
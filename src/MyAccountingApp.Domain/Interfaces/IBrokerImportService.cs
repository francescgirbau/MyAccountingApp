using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

public interface IBrokerImportService
{
    Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

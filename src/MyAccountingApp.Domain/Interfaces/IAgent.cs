using MyAccountingApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace MyAccountingApp.Domain.Interfaces;

public interface IAgent
{
    Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

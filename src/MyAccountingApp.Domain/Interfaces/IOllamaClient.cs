namespace MyAccountingApp.Core.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public interface IOllamaClient
{
    Task<string> GenerateAsync(
        string modelName,
        string prompt,
        CancellationToken cancellationToken = default);
}

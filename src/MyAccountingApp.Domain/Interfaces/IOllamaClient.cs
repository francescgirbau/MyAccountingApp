namespace MyAccountingApp.Core.Interfaces;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides access to the Ollama text generation API.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Generates a completion using the specified model and prompt.
    /// </summary>
    /// <param name="modelName">The model name to use.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw response text returned by Ollama.</returns>
    Task<string> GenerateAsync(
        string modelName,
        string prompt,
        CancellationToken cancellationToken = default);
}

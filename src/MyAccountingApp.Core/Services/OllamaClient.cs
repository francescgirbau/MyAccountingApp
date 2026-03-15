namespace MyAccountingApp.Core.Services;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Core.Interfaces;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<OllamaClient>? logger;

    public OllamaClient(HttpClient httpClient, ILogger<OllamaClient>? logger = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger;
    }

    public async Task<string> GenerateAsync(
        string modelName,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name must be provided.", nameof(modelName));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt must be provided.", nameof(prompt));
        }

        this.logger?.LogDebug("Ollama request - Model: {Model}", modelName);
        this.logger?.LogDebug("Ollama prompt: {Prompt}", prompt);

        OllamaRequest request = new OllamaRequest
        {
            Model = modelName,
            Prompt = prompt,
            Stream = false,
        };

        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(request),
        };

        using HttpResponseMessage response = await this.httpClient.SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        this.logger?.LogDebug("Ollama raw response: {Response}", content);

        OllamaResponse? result = JsonSerializer.Deserialize<OllamaResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || string.IsNullOrWhiteSpace(result.Response))
        {
            throw new InvalidOperationException("Cannot deserialize Ollama response.");
        }

        this.logger?.LogDebug("Ollama parsed response: {Response}", result.Response);

        return result.Response;
    }

    private sealed class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;
    }
}

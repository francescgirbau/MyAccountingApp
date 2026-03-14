namespace MyAccountingApp.Core.Agents;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

public class InteractiveBrokersAgent : ITransactionAgent, IAssetTransactionAgent
{
    private readonly IOllamaClient ollamaClient;
    private readonly IInteractiveBrokersPromptBuilder promptBuilder;
    private readonly string modelName;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly int maxRetries;
    private readonly int initialDelayMs;
    private readonly ILogger<InteractiveBrokersAgent> logger;

    public InteractiveBrokersAgent(
        IOllamaClient ollamaClient,
        IInteractiveBrokersPromptBuilder promptBuilder,
        string modelName,
        ILogger<InteractiveBrokersAgent> logger,
        int maxRetries = 3,
        int initialDelayMs = 1000)
    {
        this.ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        this.promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        this.modelName = string.IsNullOrWhiteSpace(modelName)
            ? throw new ArgumentException("Model name must be provided.", nameof(modelName))
            : modelName;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.maxRetries = maxRetries;
        this.initialDelayMs = initialDelayMs;

        this.jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<IEnumerable<Transaction>> ParseTransactionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await this.ParseAsync(
            filePath,
            this.promptBuilder.BuildTransactionsPrompt,
            this.DeserializeTransactions,
            this.MapToTransaction,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<AssetTransaction>> ParseAssetTransactionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await this.ParseAsync(
            filePath,
            this.promptBuilder.BuildAssetTransactionsPrompt,
            this.DeserializeAssetTransactions,
            this.MapToAssetTransaction,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<TOutput>> ParseAsync<TDto, TOutput>(
        string filePath,
        Func<string, string> promptBuilder,
        Func<string, IReadOnlyCollection<TDto>> deserializer,
        Func<TDto, TOutput> mapper,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.logger.LogInformation("Parsing file: {FilePath}", filePath);

        string csvContent = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        string prompt = promptBuilder(csvContent);

        string json = await this.ExecuteWithRetryAsync(prompt, cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<TDto> dtos = deserializer(json);

        List<TOutput> results = new List<TOutput>(dtos.Count);
        foreach (TDto dto in dtos)
        {
            results.Add(mapper(dto));
        }

        this.logger.LogInformation("Successfully parsed {Count} transactions from {FilePath}", results.Count, filePath);

        return results;
    }

    private async Task<string> ExecuteWithRetryAsync(string prompt, CancellationToken cancellationToken)
    {
        int delay = this.initialDelayMs;

        for (int attempt = 0; attempt <= this.maxRetries; attempt++)
        {
            try
            {
                this.logger.LogDebug("Calling Ollama model {ModelName}, attempt {Attempt}", this.modelName, attempt + 1);
                return await this.ollamaClient.GenerateAsync(this.modelName, prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < this.maxRetries && !cancellationToken.IsCancellationRequested)
            {
                this.logger.LogWarning(ex, "Ollama call failed, attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms", attempt + 1, this.maxRetries, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay *= 2;
            }
        }

        this.logger.LogError("Failed to generate response after {MaxRetries} attempts", this.maxRetries + 1);
        throw new InvalidOperationException($"Failed to generate response after {this.maxRetries + 1} attempts.");
    }

    private IReadOnlyCollection<TransactionResponse> DeserializeTransactions(string json)
    {
        TransactionResponseWrapper? wrapper = JsonSerializer.Deserialize<TransactionResponseWrapper>(json, this.jsonOptions);
        return wrapper?.Transactions ?? throw new InvalidOperationException("Cannot parse transactions from Ollama JSON.");
    }

    private IReadOnlyCollection<AssetTransactionResponse> DeserializeAssetTransactions(string json)
    {
        AssetTransactionResponseWrapper? wrapper = JsonSerializer.Deserialize<AssetTransactionResponseWrapper>(json, this.jsonOptions);
        return wrapper?.AssetTransactions ?? throw new InvalidOperationException("Cannot parse asset transactions from Ollama JSON.");
    }

    private Transaction MapToTransaction(TransactionResponse dto)
    {
        Money money = new Money(dto.Money.Amount, dto.Money.Currency);
        return new Transaction(
            DateTime.Parse(dto.Date, CultureInfo.InvariantCulture),
            dto.Description,
            money,
            Enum.Parse<TransactionCategory>(dto.Category, ignoreCase: true));
    }

    private AssetTransaction MapToAssetTransaction(AssetTransactionResponse dto)
    {
        Money money = new Money(dto.Money.Amount, dto.Money.Currency);
        TransactionCategory category = dto.Type.Equals("Buy", StringComparison.OrdinalIgnoreCase) || dto.Type.Equals("TaxWithholding", StringComparison.OrdinalIgnoreCase)
            ? TransactionCategory.EXPENSE
            : TransactionCategory.INCOME;

        Transaction transaction = new Transaction(
            DateOnly.Parse(dto.Date, CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue),
            dto.Description,
            money,
            category);

        return new AssetTransaction(
            transaction,
            dto.AssetName,
            dto.Type.Equals("Dividend", StringComparison.OrdinalIgnoreCase) ? 0 : dto.Quantity,
            Enum.Parse<AssetTransactionType>(dto.Type, ignoreCase: true));
    }

    private sealed class TransactionResponseWrapper
    {
        [JsonPropertyName("transactions")]
        public List<TransactionResponse>? Transactions { get; set; }
    }

    private sealed class AssetTransactionResponseWrapper
    {
        [JsonPropertyName("assetTransactions")]
        public List<AssetTransactionResponse>? AssetTransactions { get; set; }
    }
}

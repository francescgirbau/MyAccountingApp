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

public class InteractiveBrokersAgent : IAgent
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

    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.logger.LogInformation("Parsing all transactions from: {FilePath}", filePath);

        string csvContent = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        string prompt = this.promptBuilder.BuildAllTransactionsPrompt(csvContent);

        string json = await this.ExecuteWithRetryAsync(prompt, cancellationToken).ConfigureAwait(false);

        (IReadOnlyCollection<TransactionResponse> transactions, IReadOnlyCollection<AssetTransactionResponse> assetTransactions) = this.DeserializeAll(json);

        List<Transaction> mappedTransactions = new List<Transaction>();
        foreach (TransactionResponse dto in transactions)
        {
            Transaction tx = this.MapToTransaction(dto);
            if (tx != null)
            {
                mappedTransactions.Add(tx);
            }
        }

        List<AssetTransaction> mappedAssetTransactions = new List<AssetTransaction>();
        foreach (AssetTransactionResponse dto in assetTransactions)
        {
            AssetTransaction atx = this.MapToAssetTransaction(dto);
            if (atx != null)
            {
                mappedAssetTransactions.Add(atx);
            }
        }

        this.logger.LogInformation("Parsed {TransactionCount} transactions and {AssetTransactionCount} asset transactions",
            mappedTransactions.Count, mappedAssetTransactions.Count);

        return (mappedTransactions, mappedAssetTransactions);
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

    private (IReadOnlyCollection<TransactionResponse> Transactions, IReadOnlyCollection<AssetTransactionResponse> AssetTransactions) DeserializeAll(string json)
    {
        string cleanedJson = this.CleanJson(json);
        AllTransactionsWrapper? wrapper = JsonSerializer.Deserialize<AllTransactionsWrapper>(cleanedJson, this.jsonOptions);
        return (wrapper?.Transactions ?? new List<TransactionResponse>(), wrapper?.AssetTransactions ?? new List<AssetTransactionResponse>());
    }

    private string CleanJson(string json)
    {
        string result = json.Trim();

        if (result.StartsWith("```json"))
        {
            result = result.Substring(7);
        }
        else if (result.StartsWith("```"))
        {
            result = result.Substring(3);
        }

        if (result.EndsWith("```"))
        {
            result = result.Substring(0, result.Length - 3);
        }

        result = result.Trim();

        int start = result.IndexOf('{');
        int end = result.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            result = result.Substring(start, end - start + 1);
        }

        return result;
    }

    private DateTime ParseDate(string dateStr)
    {
        string cleaned = dateStr.Split(',')[0].Trim();
        return DateTime.Parse(cleaned, CultureInfo.InvariantCulture);
    }

    private Transaction MapToTransaction(TransactionResponse dto)
    {
        if (dto.Money == null || string.IsNullOrEmpty(dto.Category))
        {
            return null;
        }

        Money money = new Money(dto.Money.Amount, dto.Money.Currency);
        
        TransactionCategory category = TransactionCategory.EXPENSE;
        if (Enum.TryParse<TransactionCategory>(dto.Category, ignoreCase: true, out TransactionCategory parsedCategory))
        {
            category = parsedCategory;
        }
        else if (dto.Category?.Equals("FEE", StringComparison.OrdinalIgnoreCase) == true)
        {
            category = TransactionCategory.EXPENSE;
        }

        return new Transaction(
            this.ParseDate(dto.Date ?? DateTime.Now.ToString("yyyy-MM-dd")),
            dto.Description ?? "Unknown",
            money,
            category);
    }

    private AssetTransaction MapToAssetTransaction(AssetTransactionResponse dto)
    {
        if (dto.Money == null || string.IsNullOrEmpty(dto.Type) || string.IsNullOrEmpty(dto.Symbol))
        {
            return null;
        }

        Money money = new Money(dto.Money.Amount, dto.Money.Currency);
        TransactionCategory category = dto.Type.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            ? TransactionCategory.EXPENSE
            : TransactionCategory.INCOME;

        Transaction transaction = new Transaction(
            this.ParseDate(dto.Date),
            dto.Description,
            money,
            category);

        return new AssetTransaction(
            transaction,
            dto.Symbol,
            dto.Quantity,
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

    private sealed class AllTransactionsWrapper
    {
        [JsonPropertyName("transactions")]
        public List<TransactionResponse>? Transactions { get; set; }

        [JsonPropertyName("assetTransactions")]
        public List<AssetTransactionResponse>? AssetTransactions { get; set; }
    }
}

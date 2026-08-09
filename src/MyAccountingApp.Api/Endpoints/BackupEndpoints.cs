using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/backup", (ITransactionRepository txRepo, IPortfolioRepository pfRepo, IOptionTransactionRepository optRepo) =>
        {
            List<Transaction> transactions = txRepo.GetAll().ToList();
            List<AssetTransaction> assetTransactions = pfRepo.GetAllTransactions().ToList();
            List<OptionTransaction> optionTransactions = optRepo.GetAll().ToList();
            string json = JsonSerializer.Serialize(new { transactions, assetTransactions, optionTransactions }, new JsonSerializerOptions { WriteIndented = true });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return Results.File(bytes, "application/json", $"myaccounting-backup-{DateTime.Now:yyyyMMdd}.json");
        });

        app.MapPost($"{prefix}/backup", async (HttpRequest request, ITransactionRepository txRepo, IPortfolioRepository pfRepo, IOptionTransactionRepository optRepo, ILogger<Program> logger) =>
        {
            using StreamReader reader = new(request.Body);
            string body = await reader.ReadToEndAsync();

            try
            {
                var backup = JsonSerializer.Deserialize<BackupData>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (backup?.Transactions is null)
                {
                    return Results.BadRequest(new { error = "Invalid backup file format: 'transactions' array is required" });
                }

                txRepo.Initialize(backup.Transactions);
                pfRepo.Initialize(backup.AssetTransactions ?? new List<AssetTransaction>());
                optRepo.Initialize(backup.OptionTransactions ?? new List<OptionTransaction>());

                logger.LogInformation(
                    "Backup restored: {Count} transactions, {Count2} asset transactions, {Count3} option transactions",
                    backup.Transactions.Count,
                    backup.AssetTransactions?.Count ?? 0,
                    backup.OptionTransactions?.Count ?? 0);
                return Results.Ok(new { message = $"Restored {backup.Transactions.Count} transactions, {backup.AssetTransactions?.Count ?? 0} asset transactions, and {backup.OptionTransactions?.Count ?? 0} option transactions" });
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
            }
        });

        // Yahoo Finance symbol lookup by company name
        app.MapGet($"{prefix}/symbol-lookup", async (string name) =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.BadRequest(new { error = "Company name is required" });
            }

            using HttpClient client = new();
            string url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(name)}&quotesCount=10";

            try
            {
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                List<object> results = new();

                foreach (JsonElement quote in doc.RootElement.GetProperty("quotes").EnumerateArray())
                {
                    string? symbol = quote.TryGetProperty("symbol", out JsonElement s) ? s.GetString() : null;
                    string? longName = quote.TryGetProperty("longname", out JsonElement ln) ? ln.GetString() : null;
                    string? exchange = quote.TryGetProperty("exchange", out JsonElement ex) ? ex.GetString() : null;
                    string? quoteType = quote.TryGetProperty("quoteType", out JsonElement qt) ? qt.GetString() : null;

                    if (symbol is not null)
                    {
                        results.Add(new { symbol, name = longName ?? symbol, exchange = exchange ?? string.Empty, type = quoteType ?? string.Empty });
                    }
                }

                return Results.Ok(results);
            }
            catch (Exception)
            {
                return Results.Ok(new List<object>());
            }
        });
    }
}

record BackupData(List<Transaction> Transactions, List<AssetTransaction>? AssetTransactions, List<OptionTransaction>? OptionTransactions);

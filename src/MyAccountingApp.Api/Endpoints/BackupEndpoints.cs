using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/backup", (IVaultService vault, ITransactionRepository txRepo, IPortfolioRepository pfRepo, IOptionTransactionRepository optRepo) =>
        {
            List<Transaction> transactions = txRepo.GetAll().ToList();
            List<AssetTransaction> assetTransactions = pfRepo.GetAllTransactions().ToList();
            List<OptionTransaction> optionTransactions = optRepo.GetAll().ToList();
            string json = JsonSerializer.Serialize(new { transactions, assetTransactions, optionTransactions }, new JsonSerializerOptions { WriteIndented = true });

            bool encrypted = vault.IsUnlocked;
            byte[] payload = encrypted ? vault.Encrypt(Encoding.UTF8.GetBytes(json)) : Encoding.UTF8.GetBytes(json);
            string fileName = $"myaccounting-backup-{DateTime.Now:yyyyMMdd}.{(encrypted ? "bin" : "json")}";
            return Results.File(payload, encrypted ? "application/octet-stream" : "application/json", fileName);
        });

        app.MapPost($"{prefix}/backup", async (HttpRequest request, IVaultService vault, ITransactionRepository txRepo, IPortfolioRepository pfRepo, IOptionTransactionRepository optRepo, ILogger<Program> logger) =>
        {
            using MemoryStream ms = new();
            await request.Body.CopyToAsync(ms);
            byte[] bodyBytes = ms.ToArray();

            byte[] payload = bodyBytes;
            if (!TryParseJson(bodyBytes) && vault.IsUnlocked)
            {
                try
                {
                    payload = vault.Decrypt(bodyBytes);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Backup restore rejected: not JSON and decryption failed ({ErrorType})", ex.GetType().Name);
                    return Results.BadRequest(new { error = "Backup is neither valid JSON nor a vault-encrypted backup." });
                }
            }

            try
            {
                var backup = JsonSerializer.Deserialize<BackupData>(Encoding.UTF8.GetString(payload), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    private static bool TryParseJson(byte[] bytes)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(bytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

record BackupData(List<Transaction> Transactions, List<AssetTransaction>? AssetTransactions, List<OptionTransaction>? OptionTransactions);

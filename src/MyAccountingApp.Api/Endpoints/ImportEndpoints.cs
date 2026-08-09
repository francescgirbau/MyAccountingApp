using System.Globalization;
using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Core.Imports.Common;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapPost($"{prefix}/import", async (ImportRequest request, IImportService importService) =>
        {
            ImportResult result = await importService.ImportFromFoldersAsync(request.FolderPaths);
            return Results.Ok(result.ToDto());
        });

        app.MapPost($"{prefix}/import/upload", async (HttpContext http, IImportService importService) =>
        {
            IFormFile? file = http.Request.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "No file provided" });
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, file.FileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                ImportResult result = await importService.ImportFromFoldersAsync(new[] { tempDir });
                return Results.Ok(result.ToDto());
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        });

        // Raw CSV import: direct dump with minimal parsing, no transformations
        app.MapPost($"{prefix}/import/raw-csv", async (
            HttpContext http,
            ITransactionRepository transactionRepo,
            ITransactionValidator validator,
            ILogger<Program> logger) =>
        {
            IFormFile? file = http.Request.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "No file provided" });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            string[] lines = await reader.ReadToEndAsync().ContinueWith(t => t.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (lines.Length < 2)
            {
                return Results.BadRequest(new { error = "CSV must have a header row and at least one data row" });
            }

            List<Transaction> parsed = new();
            List<string> errors = new();
            string defaultCurrency = "EUR";

            for (int i = 1; i < lines.Length; i++)
            {
                List<string> parts = BankCsvImportService.ParseCsvLine(lines[i]);
                if (parts.Count < 4)
                {
                    errors.Add($"Line {i + 1}: expected at least 4 columns (Date,Description,Amount,Type), got {parts.Count}");
                    continue;
                }

                DateTime date;
                if (!DateTime.TryParse(parts[0], CultureInfo.CreateSpecificCulture("ca-ES"), DateTimeStyles.None, out date)
                    && !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    errors.Add($"Line {i + 1}: invalid date '{parts[0]}'");
                    continue;
                }

                string description = parts[1];

                decimal amount;
                if (!decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                {
                    errors.Add($"Line {i + 1}: invalid amount '{parts[2]}'");
                    continue;
                }

                if (!Enum.TryParse<TransactionCategory>(parts[3], ignoreCase: true, out TransactionCategory category))
                {
                    errors.Add($"Line {i + 1}: invalid type '{parts[3]}' (valid: Income, Expense, Transfer, Deposit)");
                    continue;
                }

                string currency = parts.Count >= 5 ? parts[4].ToUpperInvariant() : defaultCurrency;
                Money money = new Money(Math.Abs(amount), currency);
                Transaction transaction = new Transaction(date, description, money, category);

                ValidationResult vr = validator.Validate(transaction);
                if (!vr.IsValid)
                {
                    errors.AddRange(vr.Errors.Select(e => $"Line {i + 1}: {e.Message}"));
                    continue;
                }

                parsed.Add(transaction);
            }

            if (parsed.Count == 0)
            {
                return Results.Ok(new
                {
                    imported = 0,
                    skipped = 0,
                    errors = errors,
                    message = "No valid transactions found in CSV",
                });
            }

            List<Transaction> existing = transactionRepo.GetAll().ToList();
            existing.AddRange(parsed);
            transactionRepo.Initialize(existing);

            logger.LogInformation("Raw CSV import: {Imported} imported", parsed.Count);

            return Results.Ok(new
            {
                imported = parsed.Count,
                skipped = 0,
                errors = errors,
            });
        });

        // Clears cash transactions, portfolio data, and option transactions. Does not touch currency conversions.
        app.MapPost($"{prefix}/data/reset", (ITransactionRepository transactionRepo, IPortfolioRepository portfolioRepo, IOptionTransactionRepository optionRepo, ILogger<Program> logger) =>
        {
            int transactionCount = transactionRepo.GetAll().Count();
            int assetTransactionCount = portfolioRepo.GetAllTransactions().Count();
            int optionCount = optionRepo.GetAll().Count();

            transactionRepo.Initialize(Array.Empty<Transaction>());
            portfolioRepo.Initialize(Array.Empty<AssetTransaction>());
            optionRepo.Initialize(Array.Empty<OptionTransaction>());

            logger.LogWarning(
                "Data reset: cleared {TransactionCount} transactions, {AssetTransactionCount} asset transactions, and {OptionCount} option transactions",
                transactionCount,
                assetTransactionCount,
                optionCount);

            return Results.Ok(new
            {
                message = "Database reset completed",
                clearedTransactions = transactionCount,
                clearedAssetTransactions = assetTransactionCount,
                clearedOptionTransactions = optionCount,
            });
        });
    }
}

record ImportRequest(List<string> FolderPaths);

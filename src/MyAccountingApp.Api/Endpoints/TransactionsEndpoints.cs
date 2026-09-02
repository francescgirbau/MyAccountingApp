using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Endpoints;

public static class TransactionsEndpoints
{
    public static void MapTransactionsEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/transactions", (ITransactionRepository repo, string? categories = null) =>
        {
            IEnumerable<Transaction> transactions = repo.GetAll();

            if (!string.IsNullOrWhiteSpace(categories))
            {
                IReadOnlySet<string> parsed = categories
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                transactions = TransactionCategoryFilter.FilterByCategories(transactions, parsed, t => t.Category.ToString());
            }

            List<TransactionDto> result = transactions.Select(t => t.ToDto()).ToList();
            return Results.Ok(result);
        });

        app.MapPost($"{prefix}/transactions", (CreateTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
        {
            if (!Enum.TryParse<TransactionCategory>(request.Category, ignoreCase: true, out TransactionCategory category))
            {
                return Results.BadRequest(new { message = $"Invalid category: {request.Category}" });
            }

            if (category == TransactionCategory.FX_CONVERSION)
            {
                return Results.BadRequest(new { message = "FX_CONVERSION requires a pair; use POST /api/transactions/fx instead." });
            }

            Money money = new(request.Amount, request.Currency);
            Transaction transaction = new(request.Date, request.Description, money, category);

            ValidationResult validation = validator.Validate(transaction);
            if (!validation.IsValid)
            {
                return Results.BadRequest(validation.Errors);
            }

            repo.AddOrUpdate(transaction);
            return Results.Created($"/api/transactions/{transaction.Id}", transaction.ToDto());
        });

        app.MapPost($"{prefix}/transactions/fx", (CreateFxTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
        {
            if (request.FromAmount <= 0 || request.ToAmount <= 0)
            {
                return Results.BadRequest(new { message = "fromAmount and toAmount must be positive." });
            }

            if (string.Equals(request.FromCurrency, request.ToCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "fromCurrency and toCurrency must differ." });
            }

            if (request.Rate is <= 0)
            {
                return Results.BadRequest(new { message = "rate must be positive when provided." });
            }

            Guid pairId = Guid.NewGuid();
            string description = string.IsNullOrWhiteSpace(request.Description)
                ? $"FX {request.FromCurrency}->{request.ToCurrency}"
                : request.Description;

            Transaction outLeg = new(request.Date, description, new Money(request.FromAmount, request.FromCurrency), TransactionCategory.FX_CONVERSION);
            outLeg.SetFxPair(pairId, FxLeg.Out, request.Rate);
            Transaction inLeg = new(request.Date, description, new Money(request.ToAmount, request.ToCurrency), TransactionCategory.FX_CONVERSION);
            inLeg.SetFxPair(pairId, FxLeg.In, request.Rate);

            if (request.Rate is not null)
            {
                decimal implied = request.ToAmount / request.FromAmount;
                if (!MatchesRate(implied, request.Rate.Value) && !MatchesRate(1 / implied, request.Rate.Value))
                {
                    return Results.BadRequest(new { message = $"rate {request.Rate:G} does not match amounts (implied {implied:G}) within tolerance." });
                }
            }

            ValidationResult outValidation = validator.Validate(outLeg);
            ValidationResult inValidation = validator.Validate(inLeg);
            if (!outValidation.IsValid || !inValidation.IsValid)
            {
                return Results.BadRequest(new { outErrors = outValidation.Errors, inErrors = inValidation.Errors });
            }

            repo.AddOrUpdate(outLeg);
            repo.AddOrUpdate(inLeg);
            return Results.Created(
                $"/api/transactions?ids={outLeg.Id},{inLeg.Id}",
                new { pairId, outTransaction = outLeg.ToDto(), inTransaction = inLeg.ToDto() });
        });

        app.MapPatch($"{prefix}/transactions/batch", (BatchTransactionPatchRequest request, ITransactionCommandService service) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { message = "ids are required." });
            }

            if (request.Patch is null || request.Patch.Category is null)
            {
                return Results.BadRequest(new { message = "patch.category is required." });
            }

            BatchPatchResult result = service.PatchMany(request.Ids, request.Patch);
            return Results.Ok(result);
        });

        app.MapPost($"{prefix}/transactions/bulk-delete", (BulkDeleteRequest request, ITransactionCommandService service) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { message = "ids are required." });
            }

            BatchDeleteResult result = service.DeleteMany(request.Ids);
            return Results.Ok(result);
        });

        app.MapPut($"{prefix}/transactions/{{id:guid}}", (Guid id, CreateTransactionRequest request, ITransactionRepository repo, ITransactionValidator validator) =>
        {
            Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
            if (existing is null)
            {
                return Results.NotFound(new { id, message = "Transaction not found" });
            }

            if (!Enum.TryParse<TransactionCategory>(request.Category, ignoreCase: true, out TransactionCategory category))
            {
                return Results.BadRequest(new { message = $"Invalid category: {request.Category}" });
            }

            Money money = new(request.Amount, request.Currency);
            Transaction transaction = new(id, request.Date, request.Description, money, category);

            ValidationResult validation = validator.Validate(transaction);
            if (!validation.IsValid)
            {
                return Results.BadRequest(validation.Errors);
            }

            repo.AddOrUpdate(transaction);
            return Results.Ok(transaction.ToDto());
        });

        app.MapDelete($"{prefix}/transactions/{{id:guid}}", (Guid id, ITransactionRepository repo) =>
        {
            Transaction? existing = repo.GetAll().FirstOrDefault(t => t.Id == id);
            if (existing is null)
            {
                return Results.NotFound(new { id, message = "Transaction not found" });
            }

            repo.Delete(existing);
            return Results.NoContent();
        });

        app.MapDelete($"{prefix}/transactions/year/{{year:int}}", (int year, ITransactionRepository repo, IPortfolioRepository portfolioRepo, IOptionTransactionRepository optionRepo) =>
        {
            int transactionsRemoved = repo.DeleteByYear(year);
            int assetsRemoved = portfolioRepo.DeleteByYear(year);
            int optionsRemoved = optionRepo.DeleteByYear(year);
            return Results.Ok(new { year, deletedTransactions = transactionsRemoved, deletedAssets = assetsRemoved, deletedOptions = optionsRemoved });
        });

        app.MapGet($"{prefix}/transactions/year/{{year:int}}/count", (int year, ITransactionRepository repo, IPortfolioRepository portfolioRepo, IOptionTransactionRepository optionRepo) =>
        {
            int transactions = repo.GetAll().Count(t => t.Date.Year == year);
            int assets = portfolioRepo.GetAllTransactions().Count(a => a.Transaction.Date.Year == year);
            int options = optionRepo.GetAll().Count(o => o.Transaction.Date.Year == year);
            return Results.Ok(new { year, transactions, assets, options });
        });

        app.MapGet($"{prefix}/asset-transactions", (IPortfolioRepository repo) =>
        {
            List<AssetTransactionDto> transactions = repo.GetAllTransactions().Select(t => t.ToDto()).ToList();
            return Results.Ok(transactions);
        });

        app.MapGet($"{prefix}/asset-transactions/{{symbol}}", (string symbol, IPortfolioRepository repo) =>
        {
            List<AssetTransactionDto> transactions = repo.GetAssetTransactions(symbol).Select(t => t.ToDto()).ToList();
            return Results.Ok(transactions);
        });

        app.MapPost($"{prefix}/asset-transactions", (CreateAssetTransactionRequest request, IPortfolioRepository repo, ITransactionValidator validator) =>
        {
            if (!Enum.TryParse<TransactionCategory>(request.Category, ignoreCase: true, out TransactionCategory category))
            {
                return Results.BadRequest(new { message = $"Invalid category: {request.Category}" });
            }

            if (!Enum.TryParse<AssetTransactionType>(request.Type, ignoreCase: true, out AssetTransactionType type))
            {
                return Results.BadRequest(new { message = $"Invalid type: {request.Type}" });
            }

            Money money = new(request.Amount, request.Currency);
            Transaction transaction = new(request.Date, request.Description, money, category);

            ValidationResult validation = validator.Validate(transaction);
            if (!validation.IsValid)
            {
                return Results.BadRequest(validation.Errors);
            }

            AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
            repo.AddOrUpdate(assetTx);
            return Results.Created($"/api/asset-transactions/{transaction.Id}", assetTx.ToDto());
        });

        app.MapPut($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, CreateAssetTransactionRequest request, IPortfolioRepository repo, ITransactionValidator validator) =>
        {
            AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
            if (existing is null)
            {
                return Results.NotFound(new { id, message = "Asset transaction not found" });
            }

            if (!Enum.TryParse<TransactionCategory>(request.Category, ignoreCase: true, out TransactionCategory category))
            {
                return Results.BadRequest(new { message = $"Invalid category: {request.Category}" });
            }

            if (!Enum.TryParse<AssetTransactionType>(request.Type, ignoreCase: true, out AssetTransactionType type))
            {
                return Results.BadRequest(new { message = $"Invalid type: {request.Type}" });
            }

            Money money = new(request.Amount, request.Currency);
            Transaction transaction = new(id, request.Date, request.Description, money, category);

            ValidationResult validation = validator.Validate(transaction);
            if (!validation.IsValid)
            {
                return Results.BadRequest(validation.Errors);
            }

            AssetTransaction assetTx = new(transaction, request.Symbol, request.Quantity, type);
            repo.AddOrUpdate(assetTx);
            return Results.Ok(assetTx.ToDto());
        });

        app.MapPatch($"{prefix}/asset-transactions/batch", (BatchAssetTransactionPatchRequest request, IAssetTransactionCommandService service) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { message = "ids are required." });
            }

            if (request.Patch is null || request.Patch.Symbol is null)
            {
                return Results.BadRequest(new { message = "patch.symbol is required." });
            }

            BatchPatchResult result = service.PatchMany(request.Ids, request.Patch);
            return Results.Ok(result);
        });

        app.MapPost($"{prefix}/asset-transactions/bulk-delete", (BulkDeleteRequest request, IAssetTransactionCommandService service) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { message = "ids are required." });
            }

            BatchDeleteResult result = service.DeleteMany(request.Ids);
            return Results.Ok(result);
        });

        app.MapDelete($"{prefix}/asset-transactions/{{id:guid}}", (Guid id, IPortfolioRepository repo) =>
        {
            AssetTransaction? existing = repo.GetAllTransactions().FirstOrDefault(t => t.Transaction.Id == id);
            if (existing is null)
            {
                return Results.NotFound(new { id, message = "Asset transaction not found" });
            }

            repo.Delete(id);
            return Results.NoContent();
        });

        app.MapDelete($"{prefix}/asset-transactions/year/{{year:int}}", (int year, IPortfolioRepository portfolioRepo) =>
        {
            int removed = portfolioRepo.DeleteByYear(year);
            return Results.Ok(new { year, deletedAssets = removed });
        });

        app.MapGet($"{prefix}/asset-transactions/year/{{year:int}}/count", (int year, IPortfolioRepository portfolioRepo) =>
        {
            int assets = portfolioRepo.GetAllTransactions().Count(a => a.Transaction.Date.Year == year);
            return Results.Ok(new { year, assets });
        });

        app.MapGet($"{prefix}/option-transactions", (IOptionTransactionRepository repo) =>
        {
            List<OptionTransactionDto> transactions = repo.GetAll().Select(t => t.ToDto()).ToList();
            return Results.Ok(transactions);
        });

        app.MapGet($"{prefix}/option-transactions/{{symbol}}", (string symbol, IOptionTransactionRepository repo) =>
        {
            List<OptionTransactionDto> transactions = repo.GetAll().Where(t => t.Symbol == symbol).Select(t => t.ToDto()).ToList();
            return Results.Ok(transactions);
        });

        app.MapPatch($"{prefix}/option-transactions/batch", (BatchOptionTransactionPatchRequest request, IOptionTransactionCommandService service) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { message = "ids are required." });
            }

            if (request.Patch is null || request.Patch.Symbol is null)
            {
                return Results.BadRequest(new { message = "patch.symbol is required." });
            }

            BatchPatchResult result = service.PatchMany(request.Ids, request.Patch);
            return Results.Ok(result);
        });

        app.MapPut($"{prefix}/option-transactions/{{id:guid}}", (Guid id, UpdateOptionTransactionRequest request, IOptionTransactionRepository repo) =>
        {
            OptionTransaction? existing = repo.GetAll().FirstOrDefault(t => t.Transaction.Id == id);
            if (existing is null)
            {
                return Results.NotFound(new { id, message = "Option transaction not found" });
            }

            if (!Enum.TryParse<TransactionCategory>(request.Category, ignoreCase: true, out TransactionCategory category))
            {
                return Results.BadRequest(new { message = $"Invalid category: {request.Category}" });
            }

            if (!Enum.TryParse<AssetTransactionType>(request.Type, ignoreCase: true, out AssetTransactionType type))
            {
                return Results.BadRequest(new { message = $"Invalid type: {request.Type}" });
            }

            Money money = new(request.Amount, request.Currency);
            Transaction transaction = new(id, request.Date, request.Description, money, category);
            OptionTransaction updated = new(transaction, request.Symbol, request.Isin, request.Quantity, type);
            repo.Update(updated);
            return Results.Ok(updated.ToDto());
        });

        app.MapDelete($"{prefix}/option-transactions/{{id:guid}}", (Guid id, IOptionTransactionRepository repo) =>
        {
            bool deleted = repo.Delete(id);
            return deleted ? Results.NoContent() : Results.NotFound(new { id, message = "Option transaction not found" });
        });

        app.MapDelete($"{prefix}/option-transactions/year/{{year:int}}", (int year, IOptionTransactionRepository repo) =>
        {
            int removed = repo.DeleteByYear(year);
            return Results.Ok(new { year, deletedOptions = removed });
        });

        app.MapGet($"{prefix}/option-transactions/year/{{year:int}}/count", (int year, IOptionTransactionRepository repo) =>
        {
            int count = repo.GetAll().Count(t => t.Transaction.Date.Year == year);
            return Results.Ok(new { year, options = count });
        });
    }

    private static bool MatchesRate(decimal implied, decimal rate) =>
        Math.Abs(implied - rate) / rate <= FxConversionPairing.RateTolerance;
}

record CreateTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category);
record CreateFxTransactionRequest(DateTime Date, string FromCurrency, decimal FromAmount, string ToCurrency, decimal ToAmount, decimal? Rate = null, string? Description = null);
record CreateAssetTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category, string Symbol, decimal Quantity, string Type);
record UpdateOptionTransactionRequest(DateTime Date, string Description, decimal Amount, string Currency, string Category, string Symbol, string Isin, decimal Quantity, string Type);
record BatchAssetTransactionPatchRequest(List<Guid> Ids, AssetTransactionPatch Patch);
record BatchOptionTransactionPatchRequest(List<Guid> Ids, OptionTransactionPatch Patch);
record BatchTransactionPatchRequest(List<Guid> Ids, TransactionPatch Patch);
record BulkDeleteRequest(List<Guid> Ids);

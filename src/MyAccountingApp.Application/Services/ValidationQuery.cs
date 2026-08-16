using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

public class ValidationQuery : IValidationQuery
{
    private readonly ITransactionRepository _txRepo;
    private readonly IPortfolioRepository _pfRepo;
    private readonly ITransactionValidator _validator;
    private readonly IConversionRepository _conversionRepo;
    private readonly IMarketPriceService _marketPriceService;

    public ValidationQuery(
        ITransactionRepository txRepo,
        IPortfolioRepository pfRepo,
        ITransactionValidator validator,
        IConversionRepository conversionRepo,
        IMarketPriceService marketPriceService)
    {
        this._txRepo = txRepo;
        this._pfRepo = pfRepo;
        this._validator = validator;
        this._conversionRepo = conversionRepo;
        this._marketPriceService = marketPriceService;
    }

    public ValidationResult ValidateAll()
    {
        List<ValidationError> allErrors = new();
        List<ValidationError> allWarnings = new();

        foreach (Transaction tx in this._txRepo.GetAll())
        {
            ValidationResult vr = this._validator.Validate(tx);
            foreach (ValidationError error in vr.Errors)
            {
                allErrors.Add(new ValidationError(error.Field, error.Message, error.Severity, EntityType: "Transaction", EntityIds: new[] { tx.Id }, Date: DateOnly.FromDateTime(tx.Date)));
            }

            foreach (ValidationError warning in vr.Warnings)
            {
                allWarnings.Add(new ValidationError(warning.Field, warning.Message, warning.Severity, EntityType: "Transaction", EntityIds: new[] { tx.Id }, Date: DateOnly.FromDateTime(tx.Date)));
            }
        }

        foreach (AssetTransaction tx in this._pfRepo.GetAllTransactions())
        {
            ValidationResult vr = this._validator.Validate(tx);
            foreach (ValidationError error in vr.Errors)
            {
                allErrors.Add(new ValidationError(error.Field, error.Message, error.Severity, EntityType: "AssetTransaction", EntityIds: new[] { tx.Transaction.Id }, Symbol: tx.Symbol, Date: DateOnly.FromDateTime(tx.Transaction.Date)));
            }

            foreach (ValidationError warning in vr.Warnings)
            {
                allWarnings.Add(new ValidationError(warning.Field, warning.Message, warning.Severity, EntityType: "AssetTransaction", EntityIds: new[] { tx.Transaction.Id }, Symbol: tx.Symbol, Date: DateOnly.FromDateTime(tx.Transaction.Date)));
            }
        }

        this.AddFifoShortfallRules(allWarnings);
        this.AddUnmatchedTransferRules(allWarnings);
        this.AddDuplicateFingerprintRules(allErrors);
        this.AddMissingFxRules(allWarnings);
        this.AddSymbolNoPriceRules(allWarnings);

        return new ValidationResult(
            allErrors.Count == 0,
            allErrors,
            allWarnings);
    }

    private void AddFifoShortfallRules(List<ValidationError> warnings)
    {
        foreach (IGrouping<string, AssetTransaction> group in this._pfRepo.GetAllTransactions().GroupBy(t => t.Symbol))
        {
            FifoPosition position = FifoCalculator.Compute(group);

            if (position.UnmatchedSellQuantity > 0)
            {
                warnings.Add(new ValidationError(
                    "FIFO_SHORTFALL",
                    $"Symbol {group.Key} has {Math.Round(position.UnmatchedSellQuantity, 4):G} units sold without an open position",
                    "warning",
                    EntityType: "AssetTransaction",
                    EntityIds: group.Select(t => t.Transaction.Id).ToArray(),
                    Symbol: group.Key));
            }
        }
    }

    private void AddUnmatchedTransferRules(List<ValidationError> warnings)
    {
        HashSet<Guid> unmatched = new HashSet<Guid>(TransferDepositPairing.UnmatchedTransferIds(this._txRepo.GetAll()));

        foreach (Transaction transfer in this._txRepo.GetAll().Where(t => unmatched.Contains(t.Id)))
        {
            warnings.Add(new ValidationError(
                "UNMATCHED_TRANSFER",
                $"Transfer on {transfer.Date:yyyy-MM-dd} ({transfer.Description}) has no matching DEPOSIT within 3 days",
                "warning",
                EntityType: "Transaction",
                EntityIds: new[] { transfer.Id },
                Date: DateOnly.FromDateTime(transfer.Date)));
        }
    }

    private void AddDuplicateFingerprintRules(List<ValidationError> errors)
    {
        foreach (IGrouping<string, Transaction> group in this._txRepo.GetAll()
            .GroupBy(t => $"{t.Date:yyyy-MM-dd}|{t.Description}|{t.Money.Amount}|{t.Money.Currency}")
            .Where(g => g.Count() > 1))
        {
            Transaction first = group.First();
            errors.Add(new ValidationError(
                "DUPLICATE_FINGERPRINT",
                $"{group.Count()} transactions share the same date, description and amount ({first.Date:yyyy-MM-dd} {first.Description} {first.Money.Amount} {first.Money.Currency})",
                "error",
                EntityType: "Transaction",
                EntityIds: group.Select(t => t.Id).ToArray(),
                Date: DateOnly.FromDateTime(first.Date)));
        }
    }

    private void AddMissingFxRules(List<ValidationError> warnings)
    {
        IEnumerable<Transaction> nonEur = this._txRepo.GetAll()
            .Where(t => t.Money.Currency != "EUR");

        foreach (IGrouping<(DateOnly Date, string Currency), Transaction> group in nonEur.GroupBy(t => (DateOnly.FromDateTime(t.Date), t.Money.Currency)))
        {
            if (FxRateResolver.Resolve(this._conversionRepo, group.Key.Date) is null)
            {
                warnings.Add(new ValidationError(
                    "MISSING_FX",
                    $"No EUR conversion available for {group.Key.Date:yyyy-MM-dd} ({group.Key.Currency})",
                    "warning",
                    EntityType: "Transaction",
                    EntityIds: group.Select(t => t.Id).ToArray(),
                    Date: group.Key.Date));
            }
        }
    }

    private void AddSymbolNoPriceRules(List<ValidationError> warnings)
    {
        foreach (IGrouping<string, AssetTransaction> group in this._pfRepo.GetAllTransactions().GroupBy(t => t.Symbol))
        {
            FifoPosition position = FifoCalculator.Compute(group);

            if (position.NetQuantity <= 0)
            {
                continue;
            }

            string symbol = group.Key;
            CachedQuote? lastQuote = this._marketPriceService.GetLastQuoteAsync(symbol).GetAwaiter().GetResult();
            Money? freshPrice = this._marketPriceService.GetCachedPriceAsync(symbol).GetAwaiter().GetResult();

            if (lastQuote is null)
            {
                warnings.Add(new ValidationError(
                    "SYMBOL_NO_PRICE",
                    $"Symbol {symbol} has an open position but no market price cached",
                    "info",
                    EntityType: "AssetTransaction",
                    Symbol: symbol));
            }
            else if (freshPrice is null)
            {
                warnings.Add(new ValidationError(
                    "SYMBOL_MARKET_CLOSED",
                    $"Symbol {symbol}: market closed, using last close from {lastQuote.AsOfUtc:yyyy-MM-dd HH:mm} UTC",
                    "info",
                    EntityType: "AssetTransaction",
                    Symbol: symbol));
            }
        }
    }
}

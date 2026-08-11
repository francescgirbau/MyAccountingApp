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
            allErrors.AddRange(vr.Errors);
            allWarnings.AddRange(vr.Warnings);
        }

        foreach (AssetTransaction tx in this._pfRepo.GetAllTransactions())
        {
            ValidationResult vr = this._validator.Validate(tx);
            allErrors.AddRange(vr.Errors);
            allWarnings.AddRange(vr.Warnings);
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
                    "warning"));
            }
        }
    }

    private void AddUnmatchedTransferRules(List<ValidationError> warnings)
    {
        List<Transaction> transfers = this._txRepo.GetAll()
            .Where(t => t.Category == TransactionCategory.TRANSFER)
            .ToList();

        foreach (Transaction transfer in transfers)
        {
            bool hasPair = transfers.Any(other =>
                other.Id != transfer.Id &&
                other.Money.Amount == transfer.Money.Amount &&
                other.Money.Currency == transfer.Money.Currency &&
                Math.Abs((other.Date - transfer.Date).TotalDays) <= 3);

            if (!hasPair)
            {
                warnings.Add(new ValidationError(
                    "UNMATCHED_TRANSFER",
                    $"Transfer on {transfer.Date:yyyy-MM-dd} ({transfer.Description}) has no matching counterpart within 3 days",
                    "warning"));
            }
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
                "error"));
        }
    }

    private void AddMissingFxRules(List<ValidationError> warnings)
    {
        IEnumerable<Transaction> nonEur = this._txRepo.GetAll()
            .Where(t => t.Money.Currency != "EUR");

        foreach (Transaction tx in nonEur)
        {
            if (this._conversionRepo.GetByDate(tx.Date) is null)
            {
                warnings.Add(new ValidationError(
                    "MISSING_FX",
                    $"No EUR conversion available for {tx.Date:yyyy-MM-dd} ({tx.Money.Currency})",
                    "warning"));
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

            Money? cachedPrice = this._marketPriceService.GetCachedPriceAsync(group.Key).GetAwaiter().GetResult();

            if (cachedPrice is null)
            {
                warnings.Add(new ValidationError(
                    "SYMBOL_NO_PRICE",
                    $"Symbol {group.Key} has an open position but no market price cached",
                    "info"));
            }
        }
    }
}

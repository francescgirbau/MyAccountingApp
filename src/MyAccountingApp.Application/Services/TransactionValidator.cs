using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Services;

public class TransactionValidator : ITransactionValidator
{
    public ValidationResult Validate(Transaction transaction)
    {
        List<ValidationError> errors = new();
        List<ValidationError> warnings = new();

        if (transaction.Date > DateTime.UtcNow.AddDays(1))
        {
            errors.Add(new ValidationError(
                "Date",
                $"Transaction date {transaction.Date:yyyy-MM-dd} is in the future",
                "error"));
        }

        if (transaction.Date.Year < 2000)
        {
            warnings.Add(new ValidationError(
                "Date",
                $"Transaction date {transaction.Date:yyyy-MM-dd} is before year 2000",
                "warning"));
        }

        if (string.IsNullOrWhiteSpace(transaction.Description))
        {
            errors.Add(new ValidationError(
                "Description",
                "Transaction description cannot be empty",
                "error"));
        }

        if (transaction.Money.Amount <= 0)
        {
            errors.Add(new ValidationError(
                "Amount",
                $"Transaction amount must be positive, got {transaction.Money.Amount}",
                "error"));
        }

        if (string.IsNullOrWhiteSpace(transaction.Money.Currency) || transaction.Money.Currency.Length != 3)
        {
            errors.Add(new ValidationError(
                "Currency",
                $"Invalid currency code: '{transaction.Money.Currency}'",
                "error"));
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    public ValidationResult Validate(AssetTransaction assetTransaction)
    {
        List<ValidationError> errors = new();
        List<ValidationError> warnings = new();

        ValidationResult txResult = this.Validate(assetTransaction.Transaction);
        errors.AddRange(txResult.Errors);
        warnings.AddRange(txResult.Warnings);

        if (string.IsNullOrWhiteSpace(assetTransaction.Symbol))
        {
            errors.Add(new ValidationError(
                "Symbol",
                "Asset symbol cannot be empty",
                "error"));
        }

        if (assetTransaction.Quantity <= 0)
        {
            errors.Add(new ValidationError(
                "Quantity",
                $"Asset quantity must be positive, got {assetTransaction.Quantity}",
                "error"));
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}

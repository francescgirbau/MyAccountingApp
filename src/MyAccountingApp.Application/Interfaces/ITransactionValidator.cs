using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

public record ValidationError(string Field, string Message, string Severity);

public record ValidationResult(bool IsValid, List<ValidationError> Errors, List<ValidationError> Warnings)
{
    public static ValidationResult Valid() => new(true, new(), new());

    public static ValidationResult FromErrors(List<ValidationError> errors) =>
        new(errors.Count == 0, errors, new());
}

public interface ITransactionValidator
{
    ValidationResult Validate(Transaction transaction);

    ValidationResult Validate(AssetTransaction assetTransaction);
}

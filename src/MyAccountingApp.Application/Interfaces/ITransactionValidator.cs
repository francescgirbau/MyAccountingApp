using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Application.Interfaces;

public record ValidationError(
    string Field,
    string Message,
    string Severity,
    string EntityType = "Transaction",
    IReadOnlyList<Guid>? EntityIds = null,
    string? Symbol = null,
    DateOnly? Date = null)
{
    public string? DeepLink
    {
        get
        {
            if (this.EntityIds is { Count: > 0 })
            {
                return $"/{this.EntityPath()}?ids={string.Join(",", this.EntityIds)}";
            }

            if (this.Symbol is not null)
            {
                return $"/asset-transactions?symbol={Uri.EscapeDataString(this.Symbol)}";
            }

            return null;
        }
    }

    private string EntityPath() => this.EntityType switch
    {
        "AssetTransaction" => "asset-transactions",
        "OptionTransaction" => "option-transactions",
        _ => "transactions",
    };
}

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

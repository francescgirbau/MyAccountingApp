using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class ValidationQuery : IValidationQuery
{
    private readonly ITransactionRepository _txRepo;
    private readonly IPortfolioRepository _pfRepo;
    private readonly ITransactionValidator _validator;

    public ValidationQuery(
        ITransactionRepository txRepo,
        IPortfolioRepository pfRepo,
        ITransactionValidator validator)
    {
        _txRepo = txRepo;
        _pfRepo = pfRepo;
        _validator = validator;
    }

    public ValidationResult ValidateAll()
    {
        List<ValidationError> allErrors = new();
        List<ValidationError> allWarnings = new();

        foreach (Transaction tx in _txRepo.GetAll())
        {
            ValidationResult vr = _validator.Validate(tx);
            allErrors.AddRange(vr.Errors);
            allWarnings.AddRange(vr.Warnings);
        }

        foreach (AssetTransaction tx in _pfRepo.GetAllTransactions())
        {
            ValidationResult vr = _validator.Validate(tx);
            allErrors.AddRange(vr.Errors);
            allWarnings.AddRange(vr.Warnings);
        }

        return new ValidationResult(
            allErrors.Count == 0,
            allErrors,
            allWarnings);
    }
}

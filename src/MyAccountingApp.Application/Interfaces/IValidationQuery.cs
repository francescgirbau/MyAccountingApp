using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Application.Interfaces;

public interface IValidationQuery
{
    ValidationResult ValidateAll();
}

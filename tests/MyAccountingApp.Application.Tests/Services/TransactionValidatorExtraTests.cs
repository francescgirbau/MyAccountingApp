using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class TransactionValidatorExtraTests
{
    private readonly TransactionValidator _validator = new();

    [Fact]
    public void Validate_AmountZeroOrNegative_ReturnsError()
    {
        Transaction tx = new(Guid.NewGuid(), new DateTime(2024, 6, 1), "Test", new Money(0, "EUR"), TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Amount");
    }
}

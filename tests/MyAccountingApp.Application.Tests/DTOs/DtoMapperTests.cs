using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.DTOs;

public class DtoMapperTests
{
    [Fact]
    public void MoneyToDto_MapsCorrectly()
    {
        Money money = new(123.45m, "USD");
        MoneyDto dto = money.ToDto();

        Assert.Equal(123.45m, dto.Amount);
        Assert.Equal("USD", dto.Currency);
    }

    [Fact]
    public void TransactionToDto_MapsCorrectly()
    {
        Money money = new(100m, "EUR");
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15),
            "Test transaction",
            money,
            TransactionCategory.INCOME);

        TransactionDto dto = tx.ToDto();

        Assert.Equal(tx.Id, dto.Id);
        Assert.Equal(tx.Date, dto.Date);
        Assert.Equal("Test transaction", dto.Description);
        Assert.Equal(100m, dto.Money.Amount);
        Assert.Equal("EUR", dto.Money.Currency);
        Assert.Equal("INCOME", dto.Category);
    }

    [Fact]
    public void AssetTransactionToDto_MapsCorrectly()
    {
        Money money = new(5000m, "USD");
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 2, 1),
            "Buy AAPL",
            money,
            TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new(tx, "AAPL", 10, AssetTransactionType.Buy);

        AssetTransactionDto dto = assetTx.ToDto();

        Assert.Equal(tx.Id, dto.Transaction.Id);
        Assert.Equal("AAPL", dto.Symbol);
        Assert.Equal(10, dto.Quantity);
        Assert.Equal("Buy", dto.Type);
        Assert.Equal(500m, dto.UnitaryCost.Amount);
        Assert.Equal("USD", dto.UnitaryCost.Currency);
    }

    [Fact]
    public void ConversionToDto_MapsCorrectly()
    {
        Conversion conversion = new(
            new DateTime(2024, 3, 1),
            Currencies.EUR,
            new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } });

        ConversionDto dto = conversion.ToDto();

        Assert.Equal(conversion.Date, dto.Date);
        Assert.Equal("EUR", dto.Source);
        Assert.Equal(1.1m, dto.Quotes["USD"]);
    }

    [Fact]
    public void ImportResultToDto_MapsEmptyResult()
    {
        ImportResult result = new()
        {
            Transactions = new List<Transaction>(),
            AssetTransactions = new List<AssetTransaction>(),
            Errors = new List<string>(),
            ValidationErrors = new List<ValidationError>(),
            ValidationWarnings = new List<ValidationError>(),
            FilesProcessed = 0,
        };

        ImportResultDto dto = result.ToDto();

        Assert.Empty(dto.Transactions);
        Assert.Empty(dto.AssetTransactions);
        Assert.Empty(dto.Errors);
        Assert.Empty(dto.ValidationErrors);
        Assert.Empty(dto.ValidationWarnings);
        Assert.Equal(0, dto.FilesProcessed);
    }

    [Fact]
    public void ImportResultToDto_MapsWithData()
    {
        Money money = new(200m, "EUR");
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 4, 1),
            "Test",
            money,
            TransactionCategory.INCOME);

        ImportResult result = new()
        {
            Transactions = new List<Transaction> { tx },
            AssetTransactions = new List<AssetTransaction>(),
            Errors = new List<string> { "error1" },
            ValidationErrors = new List<ValidationError>
            {
                new("Field", "Message", "error"),
            },
            ValidationWarnings = new List<ValidationError>(),
            FilesProcessed = 2,
        };

        ImportResultDto dto = result.ToDto();

        Assert.Single(dto.Transactions);
        Assert.Equal(tx.Id, dto.Transactions[0].Id);
        Assert.Single(dto.Errors);
        Assert.Equal("error1", dto.Errors[0]);
        Assert.Single(dto.ValidationErrors);
        Assert.Equal("Field", dto.ValidationErrors[0].Field);
        Assert.Equal(2, dto.FilesProcessed);
    }
}

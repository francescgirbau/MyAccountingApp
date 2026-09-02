using System;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

namespace MyAccountingApp.Application.Tests.Services;

public class DuplicateResolutionServiceTests
{
    [Fact]
    public void PickDefaultKeeper_NoSource_PicksSmallestId()
    {
        Transaction larger = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: null);
        Transaction smaller = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: null);

        Transaction keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { larger, smaller });

        Assert.Equal(smaller.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_PrefersTransactionWithProvenance()
    {
        Transaction manual = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: null);
        Transaction imported = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: "caixa.csv");

        Transaction keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { manual, imported });

        Assert.Equal(imported.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_BothWithProvenance_PicksSmallestId()
    {
        Transaction first = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: "caixa.csv");
        Transaction second = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: "caixa.csv");

        Transaction keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { first, second });

        Assert.Equal(second.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => DuplicateResolutionService.PickDefaultKeeper(Array.Empty<Transaction>()));
    }

    [Fact]
    public void PickDefaultKeeper_AssetTransaction_PrefersProvenanceThenSmallestId()
    {
        AssetTransaction manual = new(
            CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: null),
            "AAPL",
            2m,
            AssetTransactionType.Buy);
        AssetTransaction imported = new(
            CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: "degiro.csv"),
            "AAPL",
            2m,
            AssetTransactionType.Buy);

        AssetTransaction keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { manual, imported });

        Assert.Equal(imported.Transaction.Id, keeper.Transaction.Id);
    }

    private static Transaction CreateTransaction(Guid id, string? source)
    {
        return new Transaction(
            id,
            new DateTime(2026, 8, 1),
            "Caixa #9027 Revolut top-up",
            new Money(200, "EUR"),
            TransactionCategory.EXPENSE,
            source);
    }
}

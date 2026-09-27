using MyAccountingApp.Contracts;
using MyAccountingApp.Web.Services;

namespace MyAccountingApp.Web.Tests.Services;

public class DuplicateResolutionServiceTests
{
    [Fact]
    public void PickDefaultKeeper_NoSource_PicksSmallestId()
    {
        TransactionDto larger = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: null);
        TransactionDto smaller = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: null);

        TransactionDto keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { larger, smaller });

        Assert.Equal(smaller.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_PrefersTransactionWithProvenance()
    {
        TransactionDto manual = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: null);
        TransactionDto imported = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: "caixa.csv");

        TransactionDto keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { manual, imported });

        Assert.Equal(imported.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_BothWithProvenance_PicksSmallestId()
    {
        TransactionDto first = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000002"), source: "caixa.csv");
        TransactionDto second = CreateTransaction(Guid.Parse("00000000-0000-0000-0000-000000000001"), source: "caixa.csv");

        TransactionDto keeper = DuplicateResolutionService.PickDefaultKeeper(new[] { first, second });

        Assert.Equal(second.Id, keeper.Id);
    }

    [Fact]
    public void PickDefaultKeeper_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => DuplicateResolutionService.PickDefaultKeeper(Array.Empty<TransactionDto>()));
    }

    private static TransactionDto CreateTransaction(Guid id, string? source)
    {
        return new TransactionDto(
            id,
            new DateTime(2026, 8, 1),
            "Caixa #9027 Revolut top-up",
            new MoneyDto(200, "EUR"),
            "EXPENSE",
            source);
    }
}
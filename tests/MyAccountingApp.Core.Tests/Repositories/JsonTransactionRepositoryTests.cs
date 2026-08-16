using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.ObjectMothers;

namespace MyAccountingApp.Core.Tests.Repositories;
public class JsonTransactionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonTransactionRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void Initialize_ShouldStoreTransactionsToFile()
    {
        // Arrange
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);

        Transaction expectedTransactionIncome = TransactionObjectMother.ValidIncome();
        Transaction expectedTransactionExpense = TransactionObjectMother.ValidExpense();
        Transaction expectedTransactionTransfer = TransactionObjectMother.ValidExpense();

        List<Transaction> initialTransactions = new()
        {
            expectedTransactionIncome,
            expectedTransactionExpense,
            expectedTransactionTransfer,
        };

        // Act
        repo.Initialize(initialTransactions);

        // Assert
        IEnumerable<Transaction> all = repo.GetAll();

        Transaction transactionIncome = all.First(t => t.Id == expectedTransactionIncome.Id);
        Transaction transactionExpense = all.First(t => t.Id == expectedTransactionExpense.Id);
        Transaction transactionTransfer = all.First(t => t.Id == expectedTransactionTransfer.Id);

        Assert.True(IsSameTransaction(expectedTransactionIncome, transactionIncome));
        Assert.True(IsSameTransaction(expectedTransactionExpense, transactionExpense));
        Assert.True(IsSameTransaction(expectedTransactionTransfer, transactionTransfer));
        Assert.Equal(3, all.Count());
    }

    private static bool IsSameTransaction(Transaction t1, Transaction t2)
    {
        return t1.Id == t2.Id;
    }

    [Fact]
    public void Add_ShouldPersistTransactionToFile()
    {
        // Arrange
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction transaction = TransactionObjectMother.ValidIncome();

        repo.AddOrUpdate(transaction);

        // Act
        JsonTransactionRepository repoReloaded = new JsonTransactionRepository(this._tempFile);
        Transaction? repoReloadedTransation = repoReloaded.GetAll().FirstOrDefault(transaction);

        // Assert
        Assert.NotNull(repoReloadedTransation);
    }

    [Fact]
    public void RoundTrip_ShouldPersistFxPairFields()
    {
        // Arrange
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Guid pairId = Guid.NewGuid();
        Transaction outLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(490.24m, "EUR"), TransactionCategory.FX_CONVERSION);
        outLeg.SetFxPair(pairId, FxLeg.Out, 1.1121m, "order-123");
        Transaction inLeg = new(new DateTime(2025, 1, 10), "FX EUR->USD", new Money(545.20m, "USD"), TransactionCategory.FX_CONVERSION);
        inLeg.SetFxPair(pairId, FxLeg.In, 1.1121m, "order-123");
        repo.Initialize(new[] { outLeg, inLeg });

        // Act
        JsonTransactionRepository repoReloaded = new JsonTransactionRepository(this._tempFile);
        List<Transaction> all = repoReloaded.GetAll().ToList();

        // Assert
        Transaction loadedOut = Assert.Single(all, t => t.Id == outLeg.Id);
        Transaction loadedIn = Assert.Single(all, t => t.Id == inLeg.Id);
        Assert.Equal(pairId, loadedOut.FxPairId);
        Assert.Equal(pairId, loadedIn.FxPairId);
        Assert.Equal(FxLeg.Out, loadedOut.FxLeg);
        Assert.Equal(FxLeg.In, loadedIn.FxLeg);
        Assert.Equal(1.1121m, loadedOut.FxBrokerRate);
        Assert.Equal("order-123", loadedIn.FxExternalKey);
    }

    [Fact]
    public void RoundTrip_OldJsonWithoutFxFields_LoadsWithNulls()
    {
        // Arrange
        File.WriteAllText(this._tempFile, """[{"Id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","Date":"2025-01-10T00:00:00","Description":"Plain","Money":{"Amount":100,"Currency":"EUR"},"Category":"INCOME"}]""");
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);

        // Act
        Transaction loaded = Assert.Single(repo.GetAll());

        // Assert
        Assert.Equal(TransactionCategory.INCOME, loaded.Category);
        Assert.Null(loaded.FxPairId);
        Assert.Null(loaded.FxLeg);
        Assert.Null(loaded.FxBrokerRate);
        Assert.Null(loaded.FxExternalKey);
    }

    [Fact]
    public void Delete_ShouldRemoveTransactionFromFile()
    {
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction transaction = TransactionObjectMother.ValidExpense();
        repo.AddOrUpdate(transaction);

        repo.Delete(transaction);

        JsonTransactionRepository repoReloaded = new JsonTransactionRepository(this._tempFile);
        Assert.DoesNotContain(transaction.Id, repoReloaded.GetAll().Select(t => t.Id));
    }

    [Fact]
    public void Delete_NonExistent_ShouldReturnFalse()
    {
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction tx = TransactionObjectMother.ValidIncome();

        bool result = repo.Delete(tx);

        Assert.False(result);
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveOnlyTransactionsForGivenYearFromFile()
    {
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(100, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(200, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx2023, tx2024 });

        repo.DeleteByYear(2024);

        JsonTransactionRepository reloaded = new JsonTransactionRepository(this._tempFile);
        List<Transaction> all = reloaded.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx2023.Id, all[0].Id);
    }

    [Fact]
    public void DeleteByYear_ShouldPersistRemovalToFile()
    {
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(100, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(200, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx2023, tx2024 });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(1, removed);
        JsonTransactionRepository reloaded = new JsonTransactionRepository(this._tempFile);
        Assert.Single(reloaded.GetAll());
    }
}

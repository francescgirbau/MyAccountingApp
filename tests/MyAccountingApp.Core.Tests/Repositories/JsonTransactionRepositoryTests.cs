using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
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
    public void Delete_ShouldRemoveTransactionFromFile()
    {
        JsonTransactionRepository repo = new JsonTransactionRepository(this._tempFile);
        Transaction transaction = TransactionObjectMother.ValidExpense();
        repo.AddOrUpdate(transaction);

        repo.Delete(transaction);

        JsonTransactionRepository repoReloaded = new JsonTransactionRepository(this._tempFile);
        Assert.DoesNotContain(transaction.Id, repoReloaded.GetAll().Select(t => t.Id));
    }
}

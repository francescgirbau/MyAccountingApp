using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.ObjectMothers;
namespace MyAccountingApp.Core.Tests.Repositories;

public class CompositeTransactionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public CompositeTransactionRepositoryTests()
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
    public void Initialize_ShouldPopulateMemoryRepoFromJsonRepo()
    {
        // Arrange
        Transaction transactionExpense = TransactionObjectMother.ValidExpense();
        Transaction transactionIncome = TransactionObjectMother.ValidIncome();

        File.WriteAllText(this._tempFile, System.Text.Json.JsonSerializer.Serialize(new[] { transactionExpense, transactionIncome }));

        CompositeTransactionRepository repo = new CompositeTransactionRepository(this._tempFile);

        // Act
        List<Transaction> allTransactions = repo.GetAll().ToList();

        // Assert
        Assert.Equal(2, allTransactions.Count);
        Assert.Contains(allTransactions, t => t.Id == transactionExpense.Id);
        Assert.Contains(allTransactions, t => t.Id == transactionIncome.Id);
    }

    [Fact]
    public void AddOrUpdate_ShouldAddTransactionToBothRepos()
    {
        // Arrange
        CompositeTransactionRepository repo = new CompositeTransactionRepository(this._tempFile);
        Transaction transactionExpense = TransactionObjectMother.ValidExpense();

        // Act
        repo.AddOrUpdate(transactionExpense);

        List<Transaction> all = repo.GetAll().ToList();
        string jsonContent = File.ReadAllText(this._tempFile);
        bool inJson = jsonContent.Contains(transactionExpense.Id.ToString());

        // Assert
        Assert.Single(all);
        Assert.Contains(all, t => t.Id == transactionExpense.Id);
        Assert.True(inJson);
    }

    [Fact]
    public void Delete_ShouldRemoveTransactionFromBothRepos()
    {
        // Arrange
        Transaction transactionExpense = TransactionObjectMother.ValidExpense();
        CompositeTransactionRepository repo = new CompositeTransactionRepository(this._tempFile);
        repo.AddOrUpdate(transactionExpense);

        // Act
        bool deleted = repo.Delete(transactionExpense);
        List<Transaction> all = repo.GetAll().ToList();
        string jsonContent = File.ReadAllText(this._tempFile);

        // Assert
        Assert.True(deleted);
        Assert.Empty(all);
        Assert.DoesNotContain(transactionExpense.Id.ToString(), jsonContent);
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveFromBothRepos()
    {
        CompositeTransactionRepository repo = new CompositeTransactionRepository(this._tempFile);
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(100, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(200, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx2023, tx2024 });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(1, removed);
        List<Transaction> all = repo.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx2023.Id, all[0].Id);
        string jsonContent = File.ReadAllText(this._tempFile);
        Assert.DoesNotContain(tx2024.Id.ToString(), jsonContent);
        Assert.Contains(tx2023.Id.ToString(), jsonContent);
    }
}

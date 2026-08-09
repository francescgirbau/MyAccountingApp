using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.ObjectMothers;

namespace MyAccountingApp.Core.Tests.Repositories;

public class InMemoryTransactionRepositoryTests
{
    [Fact]
    public void Initialize_ShouldStoreTransactionsInMemory()
    {
        // Arrange
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();

        Transaction transactionIncome = TransactionObjectMother.ValidIncome();
        Transaction transactionExpense = TransactionObjectMother.ValidExpense();
        Transaction transactionTransfer = TransactionObjectMother.ValidExpense();

        List<Transaction> initialTransactions = new()
        {
            transactionIncome,
            transactionExpense,
            transactionTransfer,
        };

        // Act
        repo.Initialize(initialTransactions);

        // Assert
        IEnumerable<Transaction> all = repo.GetAll();

        Assert.Contains(transactionIncome, all);
        Assert.Contains(transactionExpense, all);
        Assert.Contains(transactionTransfer, all);
        Assert.Equal(3, all.Count());
    }

    [Fact]
    public void Add_ShouldStoreTransactionsInMemory()
    {
        // Arrange
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();

        Transaction transactionIncome = TransactionObjectMother.ValidIncome();
        Transaction transactionExpense = TransactionObjectMother.ValidExpense();
        Transaction transactionTranser = TransactionObjectMother.ValidExpense();

        repo.AddOrUpdate(transactionIncome);
        repo.AddOrUpdate(transactionExpense);
        repo.AddOrUpdate(transactionTranser);

        // Act
        IEnumerable<Transaction> all = repo.GetAll();

        // Assert
        Assert.Contains(transactionIncome, all);
        Assert.Contains(transactionExpense, all);
        Assert.Contains(transactionTranser, all);
        Assert.Equal(3, all.Count());
    }

    [Fact]
    public void Delete_ShouldRemoveTransaction()
    {
        // Arrange
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();
        Transaction transaction = TransactionObjectMother.ValidTransfer();

        repo.AddOrUpdate(transaction);

        // Act
        repo.Delete(transaction);

        // Assert
        Assert.DoesNotContain(transaction, repo.GetAll());
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveOnlyTransactionsForGivenYear()
    {
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(100, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024a = new Transaction(new DateTime(2024, 3, 10), "2024a", new Money(200, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024b = new Transaction(new DateTime(2024, 7, 1), "2024b", new Money(50, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx2023, tx2024a, tx2024b });

        repo.DeleteByYear(2024);

        Assert.Single(repo.GetAll());
        Assert.Equal(tx2023.Id, repo.GetAll().First().Id);
    }

    [Fact]
    public void DeleteByYear_ShouldReturnCountOfRemovedTransactions()
    {
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();
        Transaction tx2024a = new Transaction(new DateTime(2024, 3, 10), "2024a", new Money(200, "EUR"), TransactionCategory.INCOME);
        Transaction tx2024b = new Transaction(new DateTime(2024, 7, 1), "2024b", new Money(50, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx2024a, tx2024b });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(2, removed);
    }

    [Fact]
    public void DeleteByYear_ShouldReturnZeroWhenNoTransactionsMatch()
    {
        InMemoryTransactionRepository repo = new InMemoryTransactionRepository();
        Transaction tx = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(100, "EUR"), TransactionCategory.INCOME);
        repo.Initialize(new[] { tx });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(0, removed);
        Assert.Single(repo.GetAll());
    }
}

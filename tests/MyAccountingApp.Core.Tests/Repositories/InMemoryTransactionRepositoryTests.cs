using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
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
}

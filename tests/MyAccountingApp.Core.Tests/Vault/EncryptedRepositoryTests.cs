using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.TestUtilities.ObjectMothers;
using Xunit;

namespace MyAccountingApp.Core.Tests.Vault;

public class EncryptedRepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public EncryptedRepositoryTests()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, true);
        }
    }

    [Fact]
    public void Repository_ShouldEncryptAndDecryptTransactions_WhenVaultIsUnlocked()
    {
        IVaultService vault = new VaultService(this._tempDir);
        vault.Initialize("testpass123");

        string filePath = Path.Combine(this._tempDir, "transactions.json");
        JsonTransactionRepository repo = new JsonTransactionRepository(filePath, vault);

        Transaction tx = TransactionObjectMother.ValidIncome();
        repo.Initialize(new[] { tx });

        // Verify .enc file exists and plaintext .json does not exist
        Assert.True(File.Exists(filePath + ".enc"));
        Assert.False(File.Exists(filePath));

        // Read back
        List<Transaction> all = repo.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx.Id, all[0].Id);
    }

    [Fact]
    public void Repository_ShouldThrow_WhenVaultIsLocked()
    {
        IVaultService vault = new VaultService(this._tempDir);
        vault.Initialize("testpass123");

        string filePath = Path.Combine(this._tempDir, "transactions.json");
        JsonTransactionRepository repo = new JsonTransactionRepository(filePath, vault);

        vault.Lock();

        Assert.Throws<InvalidOperationException>(() => repo.GetAll());
        Assert.Throws<InvalidOperationException>(() => repo.Initialize(Enumerable.Empty<Transaction>()));
    }

    [Fact]
    public void Repository_ShouldMigratePlaintextJson_WhenVaultIsUnlocked()
    {
        string filePath = Path.Combine(this._tempDir, "transactions.json");
        Transaction tx = TransactionObjectMother.ValidIncome();

        // Write plaintext json
        JsonSerializerOptions options = new() { WriteIndented = true };
        File.WriteAllText(filePath, JsonSerializer.Serialize(new[] { tx }, options));
        Assert.True(File.Exists(filePath));

        IVaultService vault = new VaultService(this._tempDir);
        vault.Initialize("migratetest");

        JsonTransactionRepository repo = new JsonTransactionRepository(filePath, vault);

        // Act & Assert
        List<Transaction> all = repo.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx.Id, all[0].Id);

        // Verify migration: .enc exists, original plaintext renamed to .bak and deleted
        Assert.True(File.Exists(filePath + ".enc"));
        Assert.True(File.Exists(filePath + ".bak"));
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void CompositeRepository_ShouldReloadData_AfterVaultUnlock()
    {
        IVaultService vault = new VaultService(this._tempDir);
        vault.Initialize("testpass123");

        string filePath = Path.Combine(this._tempDir, "transactions.json");
        CompositeTransactionRepository repo = new CompositeTransactionRepository(filePath, vault);

        Transaction tx = TransactionObjectMother.ValidIncome();
        repo.Initialize(new[] { tx });

        // Lock: memory is cleared alongside the vault key
        vault.Lock();
        repo.Clear();
        Assert.Empty(repo.GetAll());

        // Unlock: reload brings the persisted data back into memory
        Assert.True(vault.Unlock("testpass123"));
        repo.Reload();
        List<Transaction> all = repo.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx.Id, all[0].Id);
    }

    [Fact]
    public void CompositeRepository_ShouldStartEmpty_WhenLockedAtStartup_AndReloadOnUnlock()
    {
        IVaultService vault = new VaultService(this._tempDir);
        vault.Initialize("testpass123");

        string filePath = Path.Combine(this._tempDir, "transactions.json");
        Transaction tx = TransactionObjectMother.ValidIncome();

        // Seed while unlocked so the data is persisted encrypted
        JsonTransactionRepository jsonRepo = new JsonTransactionRepository(filePath, vault);
        jsonRepo.Initialize(new[] { tx });
        Assert.True(File.Exists(filePath + ".enc"));

        // Simulate API restart with a locked vault: constructor starts empty
        vault.Lock();
        CompositeTransactionRepository composite = new CompositeTransactionRepository(filePath, vault);
        Assert.Empty(composite.GetAll());

        // Unlock + reload brings the data back without a process restart
        Assert.True(vault.Unlock("testpass123"));
        composite.Reload();
        List<Transaction> all = composite.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal(tx.Id, all[0].Id);
    }
}

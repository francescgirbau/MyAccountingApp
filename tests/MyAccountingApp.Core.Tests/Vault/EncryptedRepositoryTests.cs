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
}

using System;
using System.IO;
using System.Text;
using MyAccountingApp.Core.Vault;
using Xunit;

namespace MyAccountingApp.Core.Tests.Vault;

public class VaultServiceTests
{
    [Fact]
    public void Vault_ShouldInitializeAndUnlock_WithCorrectPassword()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            IVaultService vault = new VaultService(tempDir);
            Assert.False(vault.IsInitialized);
            Assert.False(vault.IsUnlocked);

            vault.Initialize("securePassword123");
            Assert.True(vault.IsInitialized);
            Assert.True(vault.IsUnlocked);

            vault.Lock();
            Assert.True(vault.IsInitialized);
            Assert.False(vault.IsUnlocked);

            bool unlocked = vault.Unlock("wrongPassword");
            Assert.False(unlocked);
            Assert.False(vault.IsUnlocked);

            bool unlockedCorrect = vault.Unlock("securePassword123");
            Assert.True(unlockedCorrect);
            Assert.True(vault.IsUnlocked);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Vault_ShouldEncryptAndDecrypt_Correctly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            IVaultService vault = new VaultService(tempDir);
            vault.Initialize("mypassword");

            string originalText = "Hello, encrypted financial data!";
            byte[] plaintext = Encoding.UTF8.GetBytes(originalText);

            byte[] ciphertext = vault.Encrypt(plaintext);
            Assert.NotEqual(plaintext, ciphertext);

            byte[] decrypted = vault.Decrypt(ciphertext);
            string decryptedText = Encoding.UTF8.GetString(decrypted);

            Assert.Equal(originalText, decryptedText);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

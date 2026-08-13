using System;
using System.IO;
using System.Text;
using MyAccountingApp.Core.Vault;
using Xunit;

namespace MyAccountingApp.Core.Tests.Vault;

public class DisabledVaultServiceTests
{
    [Fact]
    public void DisabledVault_ShouldNeverBeInitializedOrUnlocked()
    {
        IVaultService vault = new DisabledVaultService();

        Assert.False(vault.IsEnabled);
        Assert.False(vault.IsInitialized);
        Assert.False(vault.IsUnlocked);
        Assert.False(vault.Unlock("anyPassword"));
    }

    [Fact]
    public void DisabledVault_ShouldLockWithoutSideEffects()
    {
        IVaultService vault = new DisabledVaultService();
        vault.Lock();
        Assert.False(vault.IsUnlocked);
    }

    [Fact]
    public void DisabledVault_ShouldThrow_OnInitialize()
    {
        IVaultService vault = new DisabledVaultService();
        Assert.Throws<InvalidOperationException>(() => vault.Initialize("securePassword123"));
    }

    [Fact]
    public void DisabledVault_ShouldThrow_OnEncryptAndDecrypt()
    {
        IVaultService vault = new DisabledVaultService();
        byte[] data = Encoding.UTF8.GetBytes("test");
        Assert.Throws<InvalidOperationException>(() => vault.Encrypt(data));
        Assert.Throws<InvalidOperationException>(() => vault.Decrypt(data));
    }
}
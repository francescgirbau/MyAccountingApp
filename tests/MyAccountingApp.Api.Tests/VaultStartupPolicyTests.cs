using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class VaultStartupPolicyTests
{
    [Fact]
    public void ResolveEnabled_ShouldReturnFalse_InDevelopment_WhenConfiguredDisabled()
    {
        bool enabled = VaultStartupPolicy.ResolveEnabled(Environments.Development, configuredEnabled: false);
        Assert.False(enabled);
    }

    [Fact]
    public void ResolveEnabled_ShouldReturnTrue_WhenConfiguredEnabled()
    {
        bool enabled = VaultStartupPolicy.ResolveEnabled(Environments.Production, configuredEnabled: true);
        Assert.True(enabled);
    }

    [Fact]
    public void ResolveEnabled_ShouldThrow_OutsideDevelopment_WhenConfiguredDisabled()
    {
        Assert.Throws<InvalidOperationException>(() => VaultStartupPolicy.ResolveEnabled(Environments.Production, configuredEnabled: false));
    }
}
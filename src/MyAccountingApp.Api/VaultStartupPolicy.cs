using Microsoft.AspNetCore.Hosting;

namespace MyAccountingApp.Api;

/// <summary>
/// Decides whether the vault feature is active for the current environment.
/// The vault must never be silently disabled outside Development: running
/// with plaintext storage in a non-dev environment is a fail-open security
/// hole, so startup is rejected instead.
/// </summary>
public static class VaultStartupPolicy
{
    public static bool ResolveEnabled(string environmentName, bool configuredEnabled)
    {
        if (configuredEnabled)
        {
            return true;
        }

        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException(
            "Vault:Enabled must be 'true' outside the Development environment: refusing to start with unencrypted storage.");
    }
}
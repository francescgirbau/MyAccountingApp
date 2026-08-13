using MyAccountingApp.Core.Persistence;

namespace MyAccountingApp.Api.Tests.Fakes;

public class NoOpVaultSessionListener : IVaultSessionListener
{
    public void OnUnlocked()
    {
    }

    public void OnLocked()
    {
    }
}

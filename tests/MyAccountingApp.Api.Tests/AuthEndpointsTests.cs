using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class AuthEndpointsTests
{
    [Fact]
    public async Task AuthWorkflow_ShouldLockAndUnlock_TheVault()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // 1. Fresh vault: not initialized, not unlocked
        HttpResponseMessage statusResp = await client.GetAsync("/api/auth/status");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        using (JsonDocument doc = JsonDocument.Parse(await statusResp.Content.ReadAsStringAsync()))
        {
            Assert.False(doc.RootElement.GetProperty("isInitialized").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("isUnlocked").GetBoolean());
        }

        // 2. Setup with a too-short password is rejected
        HttpResponseMessage weakSetupResp = await client.PostAsJsonAsync("/api/auth/setup", new { password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weakSetupResp.StatusCode);

        // 3. Setup with a valid password succeeds
        HttpResponseMessage setupResp = await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        Assert.Equal(HttpStatusCode.OK, setupResp.StatusCode);

        // 4. After setup the vault is unlocked, and a transaction can be created
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/transactions", new
        {
            date = DateTime.Today,
            description = "Test transaction",
            amount = 12.5m,
            currency = "EUR",
            category = "EXPENSE",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // 5. Lock the vault
        HttpResponseMessage lockResp = await client.PostAsJsonAsync("/api/auth/lock", new { });
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        // 6. Protected endpoints return 401 while locked
        HttpResponseMessage txResp = await client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.Unauthorized, txResp.StatusCode);
        HttpResponseMessage dashboardResp = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, dashboardResp.StatusCode);

        // 7. Wrong password does not unlock
        HttpResponseMessage wrongUnlockResp = await client.PostAsJsonAsync("/api/auth/unlock", new { password = "wrongpassword123" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongUnlockResp.StatusCode);

        // 8. Correct password unlocks and data is visible again
        HttpResponseMessage unlockResp = await client.PostAsJsonAsync("/api/auth/unlock", new { password = "testpassword123" });
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);

        HttpResponseMessage txAfterUnlock = await client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.OK, txAfterUnlock.StatusCode);
        JsonDocument txDoc = JsonDocument.Parse(await txAfterUnlock.Content.ReadAsStringAsync());
        Assert.Equal(1, txDoc.RootElement.GetArrayLength());
    }
}

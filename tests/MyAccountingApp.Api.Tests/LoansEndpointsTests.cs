using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Tests;

public class LoansEndpointsTests
{
    private static object CreateLoanBody(string direction = "Borrowed", decimal amount = 1000m)
    {
        return new
        {
            startDate = new DateTime(2025, 3, 1),
            counterparty = "Berta",
            amount,
            currency = "EUR",
            direction,
            notes = "Personal loan",
        };
    }

    private static object CreateRepaymentBody(decimal amount = 250m, decimal interestAmount = 0m)
    {
        return new
        {
            date = new DateTime(2025, 4, 1),
            amount,
            interestAmount,
        };
    }

    [Fact]
    public async Task Loans_CreateRepaymentAndDelete_EndToEnd()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        Assert.Equal(1000m, createdDocument.RootElement.GetProperty("outstanding").GetDecimal());

        HttpResponseMessage repoList = await client.GetAsync("/api/loans");
        Assert.Equal(HttpStatusCode.OK, repoList.StatusCode);
        using JsonDocument listDocument = JsonDocument.Parse(await repoList.Content.ReadAsStringAsync());
        Assert.Single(listDocument.RootElement.EnumerateArray());

        HttpResponseMessage repayment = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody());
        Assert.Equal(HttpStatusCode.OK, repayment.StatusCode);

        HttpResponseMessage byId = await client.GetAsync($"/api/loans/{loanId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        using JsonDocument byIdDocument = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        Assert.Equal(250m, byIdDocument.RootElement.GetProperty("repaid").GetDecimal());
        Assert.Equal(750m, byIdDocument.RootElement.GetProperty("outstanding").GetDecimal());

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/loans/{loanId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        HttpResponseMessage afterDelete = await client.GetAsync($"/api/loans/{loanId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Loans_GetById_ReturnsNotFound_WhenMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/loans/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_Create_InvalidDirection_ReturnsBadRequest()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/loans", CreateLoanBody(direction: "Sideways"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Loans_AddRepayment_ReturnsNotFound_WhenLoanMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/loans/{Guid.NewGuid()}/repayments", CreateRepaymentBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_Close_SetsClosedAndReturnsSummary()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        HttpResponseMessage closed = await client.PatchAsync($"/api/loans/{loanId}/close", null);

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using JsonDocument closedDocument = JsonDocument.Parse(await closed.Content.ReadAsStringAsync());
        Assert.True(closedDocument.RootElement.GetProperty("isClosed").GetBoolean());

        HttpResponseMessage byId = await client.GetAsync($"/api/loans/{loanId}");
        using JsonDocument byIdDocument = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        Assert.True(byIdDocument.RootElement.GetProperty("isClosed").GetBoolean());
    }

    [Fact]
    public async Task Loans_Close_ReturnsNotFound_WhenLoanMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PatchAsync($"/api/loans/{Guid.NewGuid()}/close", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_GetAll_IncludesMovements_OrderedByDate()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        HttpResponseMessage repayment = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody());
        Assert.Equal(HttpStatusCode.OK, repayment.StatusCode);

        HttpResponseMessage list = await client.GetAsync("/api/loans");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        JsonElement loan = Assert.Single(listDocument.RootElement.EnumerateArray());
        JsonElement.ArrayEnumerator movements = loan.GetProperty("movements").EnumerateArray();

        List<JsonElement> movementList = movements.ToList();
        Assert.Equal(2, movementList.Count);
        Assert.Equal("Disbursement", movementList[0].GetProperty("type").GetString());
        Assert.Equal(1000m, movementList[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Repayment", movementList[1].GetProperty("type").GetString());
        Assert.Equal(250m, movementList[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Loans_AddRepayment_WithInterest_ReturnsSummaryWithInterestPaid()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        HttpResponseMessage repayment = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody(amount: 200m, interestAmount: 100m));
        Assert.Equal(HttpStatusCode.OK, repayment.StatusCode);

        HttpResponseMessage byId = await client.GetAsync($"/api/loans/{loanId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        using JsonDocument byIdDocument = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        JsonElement root = byIdDocument.RootElement;
        Assert.Equal(200m, root.GetProperty("repaid").GetDecimal());
        Assert.Equal(100m, root.GetProperty("interestPaid").GetDecimal());
        Assert.Equal(800m, root.GetProperty("outstanding").GetDecimal());

        JsonElement.ArrayEnumerator movements = root.GetProperty("movements").EnumerateArray();
        List<JsonElement> movementList = movements.ToList();
        Assert.Equal(3, movementList.Count);
        Assert.Equal("Disbursement", movementList[0].GetProperty("type").GetString());
        Assert.Equal("Repayment", movementList[1].GetProperty("type").GetString());
        Assert.Equal("Interest", movementList[2].GetProperty("type").GetString());
        Assert.Equal(100m, movementList[2].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Loans_Delete_CascadesMovements()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody());

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/loans/{loanId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        ILoanMovementRepository movementRepo = factory.Services.GetRequiredService<ILoanMovementRepository>();
        Assert.Empty(movementRepo.GetByLoan(loanId));
    }

    [Fact]
    public async Task Loans_LentDirection_IsReturnedAsLent()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody(direction: "Lent", amount: 500m));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Lent", document.RootElement.GetProperty("direction").GetString());
        Assert.Equal(500m, document.RootElement.GetProperty("principal").GetDecimal());
    }

    [Fact]
    public async Task Loans_UpdateMovement_ChangesAmountDateAndType()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody(amount: 250m));
        Guid repaymentId = await GetMovementIdAsync(client, loanId, "Repayment");

        var payload = new { date = new DateTime(2025, 5, 1), amount = 300m, type = "Interest" };
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/loans/{loanId}/movements/{repaymentId}", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0m, document.RootElement.GetProperty("repaid").GetDecimal());
        Assert.Equal(300m, document.RootElement.GetProperty("interestPaid").GetDecimal());
        Assert.Equal(1000m, document.RootElement.GetProperty("outstanding").GetDecimal());
        JsonElement movement = Assert.Single(document.RootElement.GetProperty("movements").EnumerateArray(), m => m.GetProperty("id").GetGuid() == repaymentId);
        Assert.Equal("Interest", movement.GetProperty("type").GetString());
        Assert.Equal(300m, movement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Loans_UpdateMovement_ReturnsNotFound_WhenLoanMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        var payload = new { date = new DateTime(2025, 4, 1), amount = 100m, type = "Repayment" };
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/loans/{Guid.NewGuid()}/movements/{Guid.NewGuid()}", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_UpdateMovement_ReturnsNotFound_WhenMovementMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        var payload = new { date = new DateTime(2025, 4, 1), amount = 100m, type = "Repayment" };
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/loans/{loanId}/movements/{Guid.NewGuid()}", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_UpdateMovement_ReturnsBadRequest_WhenTypeInvalid()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody());
        Guid repaymentId = await GetMovementIdAsync(client, loanId, "Repayment");

        var payload = new { date = new DateTime(2025, 4, 1), amount = 100m, type = "Refund" };
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/loans/{loanId}/movements/{repaymentId}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Loans_UpdateMovement_ReturnsBadRequest_WhenEditingDisbursement()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        Guid disbursementId = await GetMovementIdAsync(client, loanId, "Disbursement");

        var payload = new { date = new DateTime(2025, 3, 1), amount = 900m, type = "Disbursement" };
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/loans/{loanId}/movements/{disbursementId}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Loans_DeleteMovement_RemovesOnlyThatMovement()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", CreateRepaymentBody());
        Guid repaymentId = await GetMovementIdAsync(client, loanId, "Repayment");

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/loans/{loanId}/movements/{repaymentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        HttpResponseMessage byId = await client.GetAsync($"/api/loans/{loanId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        Assert.Equal(0m, document.RootElement.GetProperty("repaid").GetDecimal());
        Assert.Equal(1000m, document.RootElement.GetProperty("outstanding").GetDecimal());
        List<JsonElement> movements = document.RootElement.GetProperty("movements").EnumerateArray().ToList();
        Assert.Single(movements);
        Assert.Equal("Disbursement", movements[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Loans_DeleteMovement_ReturnsNotFound_WhenMovementMissing()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/loans/{loanId}/movements/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task Loans_DeleteMovement_ReturnsBadRequest_WhenDisbursement()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", CreateLoanBody());
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();
        Guid disbursementId = await GetMovementIdAsync(client, loanId, "Disbursement");

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/loans/{loanId}/movements/{disbursementId}");

        Assert.Equal(HttpStatusCode.BadRequest, deleted.StatusCode);
    }

    private static async Task<Guid> GetMovementIdAsync(HttpClient client, Guid loanId, string type)
    {
        HttpResponseMessage byId = await client.GetAsync($"/api/loans/{loanId}");
        using JsonDocument document = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        JsonElement movement = Assert.Single(document.RootElement.GetProperty("movements").EnumerateArray(), m => m.GetProperty("type").GetString() == type);
        return movement.GetProperty("id").GetGuid();
    }
}
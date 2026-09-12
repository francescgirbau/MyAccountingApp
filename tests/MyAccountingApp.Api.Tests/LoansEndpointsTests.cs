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

    private static object CreateRepaymentBody(decimal amount = 250m)
    {
        return new
        {
            date = new DateTime(2025, 4, 1),
            amount,
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
}
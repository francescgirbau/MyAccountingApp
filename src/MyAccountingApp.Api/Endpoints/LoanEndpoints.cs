using Microsoft.AspNetCore.Builder;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;

namespace MyAccountingApp.Api.Endpoints;

public static class LoanEndpoints
{
    public static void MapLoansEndpoints(this WebApplication app)
    {
        const string prefix = ApiEndpoints.ApiPrefix;

        app.MapGet($"{prefix}/loans", (ILoanQuery loanQuery) =>
        {
            List<LoanSummaryDto> loans = loanQuery.GetAll();
            return Results.Ok(loans);
        });

        app.MapGet($"{prefix}/loans/{{id:guid}}", (Guid id, ILoanQuery loanQuery) =>
        {
            LoanSummaryDto? summary = loanQuery.GetById(id);
            return summary is not null ? Results.Ok(summary) : Results.NotFound(new { id, message = "Loan not found" });
        });

        app.MapPost($"{prefix}/loans", (CreateLoanRequest request, ILoanCommandService loanCommandService) =>
        {
            LoanSummaryDto loan;
            try
            {
                loan = loanCommandService.CreateLoan(request);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }

            return Results.Created($"/api/loans/{loan.LoanId}", loan);
        });

        app.MapPost($"{prefix}/loans/{{id:guid}}/repayments", (Guid id, AddLoanRepaymentRequest request, ILoanCommandService loanCommandService) =>
        {
            LoanSummaryDto loan;
            try
            {
                loan = loanCommandService.AddRepayment(id, request);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { id, message = "Loan not found" });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }

            return Results.Ok(loan);
        });

        app.MapPatch($"{prefix}/loans/{{id:guid}}/close", (Guid id, ILoanCommandService loanCommandService, ILoanQuery loanQuery) =>
        {
            if (!loanCommandService.CloseLoan(id))
            {
                return Results.NotFound(new { id, message = "Loan not found" });
            }

            return Results.Ok(loanQuery.GetById(id));
        });

        app.MapDelete($"{prefix}/loans/{{id:guid}}", (Guid id, ILoanCommandService loanCommandService) =>
        {
            bool deleted = loanCommandService.DeleteLoan(id);
            return deleted ? Results.NoContent() : Results.NotFound(new { id, message = "Loan not found" });
        });
    }
}
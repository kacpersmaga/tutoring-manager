namespace TutoringManager.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").RequireAuthorization();

        group.MapPost("/imports", (IFormFile? file, ImportService imports) => imports.ImportAsync(file))
            // Antiforgery is checked by UseApiAntiforgery for every /api write, form uploads included.
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2 * ImportService.MaxFileBytes));
        group.MapGet("/report", (string month, ReportService reports) => reports.GetMonthlyAsync(month));
        group.MapGet("/unmatched", (TransactionService transactions) => transactions.GetUnmatchedAsync());
        group.MapPost("/transactions/{id:guid}/assign", (Guid id, AssignTransactionRequest request, TransactionService transactions) =>
            transactions.AssignAsync(id, request));

        app.MapGet("/api/students/{id:guid}/payments", (Guid id, TransactionService transactions) => transactions.GetForStudentAsync(id))
            .RequireAuthorization();
    }
}

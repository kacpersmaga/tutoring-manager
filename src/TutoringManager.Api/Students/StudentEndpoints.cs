namespace TutoringManager.Api.Students;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/students").RequireAuthorization();

        group.MapGet("/", (bool? includeInactive, StudentService students) => students.ListAsync(includeInactive ?? false));
        group.MapGet("/{id:guid}", (Guid id, StudentService students) => students.GetAsync(id));
        group.MapPost("/", async (CreateStudentRequest request, StudentService students) =>
        {
            var student = await students.CreateAsync(request);
            return Results.Created($"/api/students/{student.Id}", student);
        });
        group.MapPut("/{id:guid}", (Guid id, UpdateStudentRequest request, StudentService students) =>
            students.UpdateAsync(id, request));
        group.MapDelete("/{id:guid}", async (Guid id, StudentService students) =>
        {
            await students.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/accounts", async (Guid id, AddBankAccountRequest request, StudentService students) =>
        {
            var account = await students.AddBankAccountAsync(id, request);
            return Results.Created($"/api/students/{id}/accounts/{account.Id}", account);
        });
        group.MapDelete("/{id:guid}/accounts/{accountId:guid}", async (Guid id, Guid accountId, StudentService students) =>
        {
            await students.DeleteBankAccountAsync(id, accountId);
            return Results.NoContent();
        });
    }
}

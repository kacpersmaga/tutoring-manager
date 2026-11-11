using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TutoringManager.Api.Infrastructure;

public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiException)
        {
            return false;
        }

        ProblemDetails problem = apiException.Errors is null
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(apiException.Errors);
        problem.Status = apiException.StatusCode;
        problem.Title = apiException.Title;
        problem.Detail = apiException.Detail;

        context.Response.StatusCode = apiException.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}

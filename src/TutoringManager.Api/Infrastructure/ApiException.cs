namespace TutoringManager.Api.Infrastructure;

/// <summary>
/// An expected error with a message for the user. Turned into a ProblemDetails response by <see cref="ApiExceptionHandler"/>.
/// </summary>
public class ApiException(int statusCode, string title, string? detail = null, IDictionary<string, string[]>? errors = null)
    : Exception(detail ?? title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static ApiException NotFound(string what) =>
        new(StatusCodes.Status404NotFound, $"{what} not found.");

    public static ApiException Conflict(string title, string? detail = null) =>
        new(StatusCodes.Status409Conflict, title, detail);

    public static ApiException Validation(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "One or more validation errors occurred.", message,
            new Dictionary<string, string[]> { [field] = [message] });
}

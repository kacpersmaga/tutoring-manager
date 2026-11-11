using System.Security.Claims;

namespace TutoringManager.Api.Infrastructure;

/// <summary>
/// The tutor making the current HTTP request. Null outside a request (background workers)
/// or for anonymous requests.
/// </summary>
public class CurrentTutor(IHttpContextAccessor httpContextAccessor)
{
    public Guid? Id
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid RequiredId => Id ?? throw new InvalidOperationException("No signed-in tutor.");
}

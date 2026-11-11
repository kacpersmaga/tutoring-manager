using Microsoft.AspNetCore.Identity;

namespace TutoringManager.Api.Auth;

public class Tutor : IdentityUser<Guid>
{
    public const string DefaultTimeZoneId = "Europe/Warsaw";

    public string DisplayName { get; set; } = "";
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;
}

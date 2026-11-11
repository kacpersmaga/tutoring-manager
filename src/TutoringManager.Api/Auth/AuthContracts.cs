using System.ComponentModel.DataAnnotations;

namespace TutoringManager.Api.Auth;

public record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(12), MaxLength(128)] string Password,
    [property: Required, MaxLength(100)] string DisplayName);

public record LoginRequest(
    [property: Required] string Email,
    [property: Required] string Password);

public record MeResponse(string Email, string DisplayName, string TimeZoneId);

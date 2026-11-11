using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Attendance;

/// <summary>
/// A link a student uses to confirm or decline one lesson without signing in.
/// Only the SHA-256 hash is stored; the raw token exists only in the email.
/// </summary>
public class AttendanceToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LessonId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>32 random bytes, Base64Url-encoded for the URL.</summary>
    public static (string RawToken, byte[] Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return (Base64Url.EncodeToString(bytes), SHA256.HashData(bytes));
    }

    /// <summary>Null when the value is not a well-formed token, so garbage never reaches the database.</summary>
    public static byte[]? HashOf(string rawToken)
    {
        if (rawToken.Length != 43 || !Base64Url.IsValid(rawToken))
        {
            return null;
        }

        return SHA256.HashData(Base64Url.DecodeFromChars(rawToken));
    }
}

public class AttendanceTokenConfiguration : IEntityTypeConfiguration<AttendanceToken>
{
    public void Configure(EntityTypeBuilder<AttendanceToken> token)
    {
        token.HasIndex(t => t.TokenHash).IsUnique();
        token.HasOne<Lesson>().WithMany().HasForeignKey(t => t.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}

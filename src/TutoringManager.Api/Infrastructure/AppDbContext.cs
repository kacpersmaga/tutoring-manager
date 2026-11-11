using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Auth;

namespace TutoringManager.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options, CurrentTutor currentTutor)
    : IdentityDbContext<Tutor, IdentityRole<Guid>, Guid>(options)
{
    // Read by the global query filters on every query, so each tutor only ever sees their own rows.
    private Guid? CurrentTutorId => currentTutor.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tutor>(tutor =>
        {
            tutor.Property(t => t.DisplayName).HasMaxLength(100);
            tutor.Property(t => t.TimeZoneId).HasMaxLength(64);
        });
    }
}

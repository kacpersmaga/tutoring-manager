using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Auth;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options, CurrentTutor currentTutor)
    : IdentityDbContext<Tutor, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentBankAccount> StudentBankAccounts => Set<StudentBankAccount>();

    // Read by the global query filters on every query, so a tutor only ever sees their own rows.
    // Background workers have no signed-in tutor and use IgnoreQueryFilters() explicitly.
    private Guid? CurrentTutorId => currentTutor.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<Tutor>(tutor =>
        {
            tutor.Property(t => t.DisplayName).HasMaxLength(100);
            tutor.Property(t => t.TimeZoneId).HasMaxLength(64);
        });

        builder.Entity<Student>().HasQueryFilter(s => s.TutorId == CurrentTutorId);
        builder.Entity<StudentBankAccount>().HasQueryFilter(a => a.TutorId == CurrentTutorId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TutoringManager.Api.Lessons;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> lesson)
    {
        lesson.Property(l => l.TimeZoneId).HasMaxLength(64);
        lesson.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        lesson.Property(l => l.Attendance).HasConversion<string>().HasMaxLength(20);
        lesson.Property(l => l.PricePerLesson).HasPrecision(10, 2);
        lesson.Property(l => l.Version).IsRowVersion();

        lesson.HasOne(l => l.Student).WithMany().HasForeignKey(l => l.StudentId).OnDelete(DeleteBehavior.Cascade);
        lesson.HasOne(l => l.Note).WithOne().HasForeignKey<LessonNote>(n => n.LessonId).OnDelete(DeleteBehavior.Cascade);

        lesson.HasIndex(l => new { l.TutorId, l.StartUtc });
        lesson.HasIndex(l => new { l.TutorId, l.LocalDate });
        lesson.HasIndex(l => l.StudentId);
        // The no-overlap exclusion constraint is added in the migration with raw SQL (EF Core has no API for it).
    }
}

public class LessonNoteConfiguration : IEntityTypeConfiguration<LessonNote>
{
    public void Configure(EntityTypeBuilder<LessonNote> note)
    {
        note.Property(n => n.Content).HasMaxLength(LessonNote.MaxLength);
        note.HasIndex(n => n.LessonId).IsUnique();
    }
}

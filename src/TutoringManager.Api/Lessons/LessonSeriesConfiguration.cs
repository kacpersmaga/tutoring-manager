using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Lessons;

public class LessonSeriesConfiguration : IEntityTypeConfiguration<LessonSeries>
{
    public void Configure(EntityTypeBuilder<LessonSeries> series)
    {
        series.ToTable("lesson_series");
        series.Property(s => s.DayOfWeek).HasConversion<string>().HasMaxLength(10);
        series.Property(s => s.TimeZoneId).HasMaxLength(64);
        series.HasOne<Student>().WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);
        series.HasMany<Lesson>().WithOne().HasForeignKey(l => l.SeriesId).OnDelete(DeleteBehavior.SetNull);
        series.HasIndex(s => s.TutorId);
    }
}

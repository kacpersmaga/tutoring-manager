using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Payments;

public record StudentMonthReport(
    Guid StudentId,
    string FullName,
    int LessonsCompleted,
    decimal OpeningBalance,
    decimal DueInMonth,
    decimal PaidInMonth,
    decimal ClosingBalance);

public record MonthlyReport(
    string Month,
    decimal DueInMonth,
    decimal PaidInMonth,
    decimal TotalOutstanding,
    List<StudentMonthReport> Students);

/// <summary>
/// Running balance per student instead of "has this month been paid?": balance = everything paid − everything due,
/// up to the end of the month. Prepayments, late payments and one transfer covering two months need no special rules.
/// Due = completed lessons × the price stored on each lesson.
/// </summary>
public class ReportService(AppDbContext db)
{
    public async Task<MonthlyReport> GetMonthlyAsync(string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
        {
            throw ApiException.Validation("month", "Use the format YYYY-MM, e.g. 2027-01.");
        }

        var last = first.AddMonths(1).AddDays(-1);

        var lessons = await db.Lessons
            .Where(l => l.Status == LessonStatus.Completed && l.LocalDate <= last)
            .GroupBy(l => l.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                DueBefore = g.Where(l => l.LocalDate < first).Sum(l => l.PricePerLesson),
                DueInMonth = g.Where(l => l.LocalDate >= first).Sum(l => l.PricePerLesson),
                CountInMonth = g.Count(l => l.LocalDate >= first),
            })
            .ToDictionaryAsync(x => x.StudentId);

        var payments = await db.BankTransactions
            .Where(t => t.StudentId != null && t.BookingDate <= last)
            .GroupBy(t => t.StudentId!.Value)
            .Select(g => new
            {
                StudentId = g.Key,
                PaidBefore = g.Where(t => t.BookingDate < first).Sum(t => t.Amount),
                PaidInMonth = g.Where(t => t.BookingDate >= first).Sum(t => t.Amount),
            })
            .ToDictionaryAsync(x => x.StudentId);

        var students = await db.Students.OrderBy(s => s.FullName).Select(s => new { s.Id, s.FullName, s.IsActive }).ToListAsync();

        var rows = new List<StudentMonthReport>();
        foreach (var student in students)
        {
            var due = lessons.GetValueOrDefault(student.Id);
            var paid = payments.GetValueOrDefault(student.Id);
            var opening = (paid?.PaidBefore ?? 0) - (due?.DueBefore ?? 0);
            var dueInMonth = due?.DueInMonth ?? 0;
            var paidInMonth = paid?.PaidInMonth ?? 0;
            var closing = opening + paidInMonth - dueInMonth;

            // Inactive students without anything to settle would only add noise.
            if (!student.IsActive && closing == 0 && dueInMonth == 0 && paidInMonth == 0)
            {
                continue;
            }

            rows.Add(new StudentMonthReport(student.Id, student.FullName, due?.CountInMonth ?? 0,
                opening, dueInMonth, paidInMonth, closing));
        }

        return new MonthlyReport(
            first.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            rows.Sum(r => r.DueInMonth),
            rows.Sum(r => r.PaidInMonth),
            rows.Where(r => r.ClosingBalance < 0).Sum(r => -r.ClosingBalance),
            rows);
    }
}

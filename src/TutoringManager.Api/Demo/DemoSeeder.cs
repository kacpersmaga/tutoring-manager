using System.Numerics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Auth;
using TutoringManager.Api.Emails;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;
using TutoringManager.Api.Payments;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Demo;

public class DemoOptions
{
    /// <summary>Only for running locally: creates a demo account with realistic data on first start.</summary>
    public bool Enabled { get; set; }
    public string Email { get; set; } = "demo@tutoring-manager.local";
    public string Password { get; set; } = "";
}

/// <summary>
/// Builds the demo account: nine students, weekly lessons from six weeks ago to twelve weeks ahead, notes,
/// answers, an imported statement and two transfers waiting to be assigned. Dates are relative to today,
/// so the demo always looks current. Runs without a signed-in user, so tutor ids are set explicitly.
/// </summary>
public class DemoSeeder(
    AppDbContext db,
    UserManager<Tutor> users,
    TimeProvider time,
    Microsoft.Extensions.Options.IOptions<DemoOptions> options,
    ILogger<DemoSeeder> logger)
{
    private const int WeeksBack = 6;

    // Rotates the answers over the upcoming lessons, so the next days show every state the calendar knows.
    private int _upcomingAnswered;

    public async Task EnsureSeededAsync()
    {
        var settings = options.Value;
        if (await users.FindByEmailAsync(settings.Email) is not null)
        {
            return;
        }

        var tutor = new Tutor { UserName = settings.Email, Email = settings.Email, DisplayName = "Maria Kowalczyk" };
        var result = await users.CreateAsync(tutor, settings.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not create the demo account: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await SeedDataAsync(tutor);
        logger.LogInformation("Demo account created with {Students} students", DemoData.Students.Length);
    }

    public bool IsDemoTutor(Tutor tutor) =>
        options.Value.Enabled && string.Equals(tutor.Email, options.Value.Email, StringComparison.OrdinalIgnoreCase);

    /// <summary>Puts the demo account back to its starting state, dated from today.</summary>
    public async Task ResetAsync(Tutor tutor)
    {
        await db.BankTransactions.IgnoreQueryFilters().Where(t => t.TutorId == tutor.Id).ExecuteDeleteAsync();
        await db.BankImports.IgnoreQueryFilters().Where(i => i.TutorId == tutor.Id).ExecuteDeleteAsync();
        // Lessons, notes, series, tokens, emails and accounts go with the students (cascade).
        await db.Students.IgnoreQueryFilters().Where(s => s.TutorId == tutor.Id).ExecuteDeleteAsync();
        await SeedDataAsync(tutor);
    }

    private async Task SeedDataAsync(Tutor tutor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = LessonTime.Today(time, tutor.TimeZoneId);
        var firstMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - WeeksBack * 7);
        var horizon = LessonSeries.HorizonFrom(today);
        var random = new Random(2027);

        var import = new BankImport
        {
            TutorId = tutor.Id,
            FileName = "mbank-demo-statement.csv",
            FileSha256 = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()),
            BankFormat = "mBank",
            ImportedAt = now.AddDays(-2),
        };
        db.BankImports.Add(import);

        for (var index = 0; index < DemoData.Students.Length; index++)
        {
            var cast = DemoData.Students[index];
            var student = new Student
            {
                TutorId = tutor.Id,
                FullName = cast.FullName,
                Email = cast.Email,
                RatePerLesson = cast.Rate,
                CreatedAt = now.AddDays(-WeeksBack * 7 - 3),
            };
            var account = AccountFromSeed(cast.AccountSeed);
            student.BankAccounts.Add(new StudentBankAccount { TutorId = tutor.Id, AccountNumber = account });
            db.Students.Add(student);

            var series = new LessonSeries
            {
                TutorId = tutor.Id,
                StudentId = student.Id,
                DayOfWeek = cast.Day,
                StartTime = cast.Start,
                DurationMinutes = cast.DurationMinutes,
                TimeZoneId = tutor.TimeZoneId,
                StartDate = firstMonday,
                GeneratedUntil = horizon,
            };
            db.LessonSeries.Add(series);

            var lessons = series.OccurrencesBetween(firstMonday, horizon)
                .Select(date => CreateLesson(tutor, student, series, date))
                .ToList();
            DecorateLessons(lessons, cast, index, now, random);
            db.Lessons.AddRange(lessons);

            AddPayments(import, student, account, lessons, index, today);
        }

        AddUnmatchedTransfers(import, today);
        await db.SaveChangesAsync();
    }

    private static Lesson CreateLesson(Tutor tutor, Student student, LessonSeries series, DateOnly date)
    {
        var lesson = new Lesson
        {
            TutorId = tutor.Id,
            StudentId = student.Id,
            SeriesId = series.Id,
            SeriesOccurrenceDate = date,
            PricePerLesson = student.RatePerLesson,
        };
        lesson.SetTime(date, series.StartTime, series.DurationMinutes, series.TimeZoneId);
        return lesson;
    }

    /// <summary>Past lessons: completed with a note (a few cancelled). Next three days: a mix of answers. Later: waiting.</summary>
    private void DecorateLessons(List<Lesson> lessons, DemoData.DemoStudent cast, int studentIndex, DateTime now, Random random)
    {
        var topics = DemoData.NoteTopics[cast.Subject];
        var pastCount = 0;

        foreach (var lesson in lessons)
        {
            if (lesson.EndUtc <= now)
            {
                var cancelled = random.Next(14) == 0;
                lesson.Attendance = cancelled ? AttendanceResponse.Declined : AttendanceResponse.Confirmed;
                lesson.AttendanceRespondedAt = lesson.StartUtc.AddHours(-20);
                lesson.Status = cancelled ? LessonStatus.Cancelled : LessonStatus.Completed;
                lesson.IsModifiedFromSeries = cancelled;
                AddEmail(lesson, cast.Email, EmailStatus.Sent, now);
                if (!cancelled)
                {
                    db.LessonNotes.Add(new LessonNote
                    {
                        TutorId = lesson.TutorId,
                        LessonId = lesson.Id,
                        Content = topics[(pastCount + studentIndex) % topics.Length],
                        CreatedAt = lesson.EndUtc,
                        UpdatedAt = lesson.EndUtc,
                    });
                    pastCount++;
                }
            }
            else if (lesson.StartUtc - now <= TimeSpan.FromDays(3))
            {
                switch (_upcomingAnswered++ % 4)
                {
                    case 0:
                        Answer(lesson, AttendanceResponse.Confirmed, now);
                        AddEmail(lesson, cast.Email, EmailStatus.Sent, now);
                        break;
                    case 1:
                        Answer(lesson, AttendanceResponse.Declined, now);
                        AddEmail(lesson, cast.Email, EmailStatus.Sent, now);
                        break;
                    case 2:
                        // Due right away on purpose: the worker sends it to Mailpit seconds after start,
                        // so the demo has a real email with a working link.
                        AddEmail(lesson, cast.Email, EmailStatus.Pending, now, sendNow: true);
                        break;
                    default:
                        AddEmail(lesson, cast.Email, EmailStatus.Sent, now);
                        break;
                }
            }
            else
            {
                AddEmail(lesson, cast.Email, EmailStatus.Pending, now);
            }
        }
    }

    private static void Answer(Lesson lesson, AttendanceResponse response, DateTime now)
    {
        lesson.Attendance = response;
        lesson.AttendanceRespondedAt = now.AddHours(-2);
    }

    private void AddEmail(Lesson lesson, string to, EmailStatus status, DateTime now, bool sendNow = false)
    {
        var sendAt = sendNow || (status == EmailStatus.Pending && lesson.StartUtc.AddHours(-24) < now) ? now : lesson.StartUtc.AddHours(-24);
        db.ScheduledEmails.Add(new ScheduledEmail
        {
            TutorId = lesson.TutorId,
            LessonId = lesson.Id,
            Type = EmailType.AttendanceRequest,
            ToEmail = to,
            SendAtUtc = sendAt,
            NextAttemptAtUtc = sendAt,
            Status = status,
            Attempts = status == EmailStatus.Sent ? 1 : 0,
            SentAtUtc = status == EmailStatus.Sent ? sendAt : null,
        });
    }

    /// <summary>
    /// Most students pay every two weeks for two lessons. Jakub is behind, Maja paid ahead,
    /// and Hanna's last transfer came from her mother's account, so it waits to be assigned.
    /// </summary>
    private void AddPayments(BankImport import, Student student, string account, List<Lesson> lessons, int index, DateOnly today)
    {
        var completed = lessons.Where(l => l.Status == LessonStatus.Completed).OrderBy(l => l.LocalDate).ToList();
        var pairs = completed.Chunk(2).ToList();
        if (student.FullName.StartsWith("Jakub"))
        {
            pairs = pairs.SkipLast(1).ToList(); // one payment behind
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            var date = pair[^1].LocalDate.AddDays(1 + index % 3);
            if (date > today)
            {
                date = today;
            }

            var fromParent = student.FullName.StartsWith("Hanna") && i == pairs.Count - 1;
            var amount = pair.Sum(l => l.PricePerLesson);
            if (student.FullName.StartsWith("Maja") && i == pairs.Count - 1)
            {
                amount += 2 * student.RatePerLesson; // paid ahead for two lessons
            }

            AddTransfer(import, date, amount,
                fromParent ? AccountFromSeed(DemoData.ParentAccountSeed) : account,
                fromParent ? DemoData.ParentName : student.FullName.ToUpperInvariant(),
                fromParent ? "Lekcje Hani" : $"Korepetycje {student.FullName.Split(' ')[0]}",
                fromParent ? null : student.Id);
        }
    }

    private void AddUnmatchedTransfers(BankImport import, DateOnly today) =>
        AddTransfer(import, today.AddDays(-4), 45.50m, AccountFromSeed("1750001300000044500"), "PIOTR ZIELIŃSKI", "Zwrot za bilety", null);

    private void AddTransfer(BankImport import, DateOnly date, decimal amount, string account, string name, string title, Guid? studentId)
    {
        var transaction = new BankTransaction
        {
            TutorId = import.TutorId,
            ImportId = import.Id,
            BookingDate = date,
            Amount = amount,
            CounterpartyAccount = account,
            CounterpartyName = name,
            Title = title,
            TransactionKey = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()),
        };
        if (studentId is { } id)
        {
            transaction.AssignTo(id, MatchedBy.Account);
        }

        db.BankTransactions.Add(transaction);
    }

    /// <summary>Turns a digit seed into a valid 26-digit Polish account number (correct IBAN check digits).</summary>
    public static string AccountFromSeed(string seed)
    {
        var bban = (seed + "000000000000000000000000")[..24];
        var check = 98 - (int)(BigInteger.Parse(bban + "252100") % 97);
        return $"{check:00}{bban}";
    }
}

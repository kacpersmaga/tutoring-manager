using System.Globalization;
using System.Net;

namespace TutoringManager.Api.Emails;

public static class AttendanceEmail
{
    public static EmailMessage Create(string to, string tutorName, DateOnly date, TimeOnly startTime, string link)
    {
        var when = $"{date.ToString("dddd, d MMMM", CultureInfo.InvariantCulture)} at {startTime:HH\\:mm}";
        var subject = $"Are you coming? Lesson on {date.ToString("ddd d MMM", CultureInfo.InvariantCulture)} at {startTime:HH\\:mm}";

        var text = $"""
            Hi,

            you have a lesson with {tutorName} on {when}.
            Please let {tutorName} know whether you will be there:

            {link}

            You can change your answer until the lesson starts.
            """;

        var safeTutor = WebUtility.HtmlEncode(tutorName);
        var html = $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:24px;background:#efe9e3;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#2b2522">
              <table role="presentation" width="100%" style="max-width:480px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px">
                <tr><td>
                  <p style="margin:0 0 8px;font-size:14px;color:#6f6560">Lesson with {safeTutor}</p>
                  <h1 style="margin:0 0 24px;font-size:22px;line-height:1.3">{WebUtility.HtmlEncode(when)}</h1>
                  <p style="margin:0 0 24px;font-size:16px;line-height:1.5">Will you be there? One tap lets {safeTutor} know.</p>
                  <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;background:#2b2522;color:#f6f2ec;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600">Answer now</a>
                  <p style="margin:24px 0 0;font-size:13px;color:#6f6560">You can change your answer until the lesson starts.</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return new EmailMessage(to, subject, text, html);
    }
}

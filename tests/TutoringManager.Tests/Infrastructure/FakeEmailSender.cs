using System.Collections.Concurrent;
using TutoringManager.Api.Emails;

namespace TutoringManager.Tests.Infrastructure;

public class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private int _failuresLeft;

    /// <summary>The next <paramref name="count"/> sends throw, like an SMTP server that is down.</summary>
    public void FailNext(int count) => Interlocked.Exchange(ref _failuresLeft, count);

    public List<EmailMessage> SentTo(string address) => _sent.Where(m => m.To == address).ToList();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
        {
            throw new IOException("SMTP server unavailable");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;
using lmsPortalBe.Services;

namespace lmsPortalBe.Tests;

/// <summary>
/// Test double for <see cref="IEmailSender"/> that records the emails it would
/// have sent, so tests can exercise the password-reset flow end to end without
/// talking to a real mail provider.
/// </summary>
public class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string To, string ResetLink)> _sent = new();

    public IReadOnlyCollection<(string To, string ResetLink)> Sent => _sent.ToArray();

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue((toEmail, resetLink));
        return Task.CompletedTask;
    }
}

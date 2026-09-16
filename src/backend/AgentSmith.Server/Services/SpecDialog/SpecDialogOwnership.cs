using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-9033: who a spec dialog on the dashboard channel belongs to, and who may
/// therefore speak into it or watch it. Sessions are keyed by (platform, threadId) and the
/// session store never reads UserId — on Slack the channel supplied the boundary, and a
/// browser-minted dialog id supplies none. A second principal who learned one could
/// otherwise post into someone else's design conversation and approve its filing, on the
/// one surface that files real tickets.
/// </summary>
public sealed class SpecDialogOwnership(
    SpecDialogSessionRepository sessions, SpecCommandParser parser)
{
    /// <summary>
    /// The owner an unauthenticated caller is recorded as. An installation that has not
    /// switched enforcement on has one shared owner here — the same posture every other
    /// route has there, decided by that one switch rather than by a second copy of it.
    /// </summary>
    public const string AnonymousOwner = "anonymous";

    /// <summary>
    /// The principal this caller is, as a session owner. The subject claim comes first
    /// because a display name can be re-issued while <c>sub</c> is what the directory
    /// guarantees stable — and it is what a grant is written against.
    /// </summary>
    public string OwnerOf(ClaimsPrincipal? caller) =>
        caller?.FindFirst("sub")?.Value ?? caller?.Identity?.Name ?? AnonymousOwner;

    /// <summary>
    /// Whether this principal may see what is delivered into the dialog. An id with no
    /// open session is free to take: it is the id a page mints for the dialog it is about
    /// to open, and nothing is delivered into it until somebody opens one.
    /// </summary>
    public async Task<bool> MayWatchAsync(string dialogId, string owner, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(
            DispatcherDefaults.PlatformDashboard, dialogId, ct);
        return session is null || session.UserId == owner;
    }

    /// <summary>
    /// Whether this principal may send this text into this dialog. "/spec resume &lt;id&gt;"
    /// re-binds an EXISTING session onto this dialog id, so the resumed session's owner is
    /// checked as well — an unguarded resume is the same takeover as posting into the
    /// other principal's dialog directly.
    /// </summary>
    public async Task<bool> MayPostAsync(
        string dialogId, string text, string owner, CancellationToken ct)
    {
        if (!await MayWatchAsync(dialogId, owner, ct)) return false;
        if (parser.Parse(text) is not SpecResumeCommand resume) return true;

        var resumed = await sessions.GetBySessionIdAsync(resume.SessionId, ct);
        return resumed is null || resumed.UserId == owner;
    }
}

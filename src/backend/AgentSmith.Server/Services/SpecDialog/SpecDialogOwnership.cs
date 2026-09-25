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
public sealed class SpecDialogOwnership(SpecDialogSessionRepository sessions)
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
    /// Whether this principal may see what is delivered into the dialog — and, because
    /// speaking into a dialog is delivered into it, whether they may POST there too.
    /// An id with no open session is free to take: it is the id a page mints for the dialog
    /// it is about to open, and nothing is delivered into it until somebody opens one.
    /// <para>
    /// 2026-09-22-2a86: the RESUME route asks this about the dialog it is moving a
    /// conversation ONTO. It used to be reached by parsing a posted "/spec resume &lt;id&gt;",
    /// and the resumer's own guards are all about the source session — so the check that the
    /// target thread is the caller's had to be carried by the route when the spelling went,
    /// or a caller could resume their own conversation onto another principal's live dialog
    /// and close whatever was open there.
    /// </para>
    /// </summary>
    public async Task<bool> MayWatchAsync(string dialogId, string owner, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(
            DispatcherDefaults.PlatformDashboard, dialogId, ct);
        return session is null || MayReach(session, owner);
    }

    /// <summary>
    /// 2026-09-25-8e51b: a conversation that belongs to a TICKET is reachable by anyone who may
    /// reach this surface at all, and an unbound one stays its owner's.
    /// <para>
    /// This is a deliberate widening and the reason is that the alternative is worse. A ticket has
    /// ONE conversation because the approval record it amends is keyed by ticket and upserts in
    /// place; two per-person conversations about one ticket would be two drafts of one artifact
    /// with a silent last-writer-wins. Owner-scoping the shared one would instead answer a second
    /// principal with a 404 for a ticket they can see on the board. The threat the class header
    /// names is accepted here knowingly: the people who can reach a project's tickets are the
    /// people who may discuss them. DELETING one is still the owner's alone.
    /// </para>
    /// </summary>
    public static bool MayReach(
        Infrastructure.Persistence.Entities.SpecDialogSession session, string owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.UserId == owner || session.TicketKey is not null;
    }

    /// <inheritdoc cref="MayReach"/>
    public async Task<bool> MayReachSessionAsync(string sessionId, string owner, CancellationToken ct)
    {
        var session = await sessions.GetBySessionOnPlatformAsync(
            DispatcherDefaults.PlatformDashboard, sessionId, ct);
        return session is not null && MayReach(session, owner);
    }

    /// <summary>
    /// 2026-09-18-7a05: whether this principal may DELETE this conversation. Keyed on the
    /// session id and scoped to the dashboard platform, and an absent row is a REFUSAL — the
    /// opposite of the watch check above, and deliberately not that check.
    /// <para>
    /// The watch check is keyed on the THREAD id and passes on a miss, because the id a page
    /// mints for the dialog it is about to open has nothing delivered into it yet. A delete
    /// opens nothing, so no such id exists here. Reusing it would authorise a delete twice
    /// over: a dialog id is client-supplied and checked only for emptiness, so a caller
    /// passing another principal's SESSION id meets a lookup that either misses — and passes —
    /// or finds the conversation they themselves opened under it, which they own.
    /// </para>
    /// </summary>
    public Task<bool> MayDeleteAsync(string sessionId, string owner, CancellationToken ct) =>
        OwnsAsync(sessionId, owner, ct);

    /// <summary>
    /// 2026-09-20-3af8: whether this principal owns the conversation with this SESSION id.
    /// An absent row is a refusal, which is what makes it safe for a write and for serving
    /// back what a write stored.
    /// </summary>
    public async Task<bool> OwnsAsync(string sessionId, string owner, CancellationToken ct)
    {
        var session = await sessions.GetBySessionOnPlatformAsync(
            DispatcherDefaults.PlatformDashboard, sessionId, ct);
        return session is not null && session.UserId == owner;
    }
}

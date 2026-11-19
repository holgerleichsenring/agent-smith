using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-10-ccd9: the decision state of a conversation's proposals, changed only by compare-and-set so two replicas or
/// two surfaces cannot both decide one card. 2026-11-17-a0b6d: each proposal is a row of <see cref="SpecDialogProposal"/>
/// with its id and state; the session row keeps none. Every write acts on the rows of the thread's NEWEST open session,
/// the one every read here answers from. At most one row awaits per session — the one-awaiting index refuses a second.
/// </summary>
public sealed class SpecDialogProposalClaim(IUnitOfWork unitOfWork)
{
    public const string Awaiting = "awaiting";
    public const string Filing = "filing";
    public const string Filed = "filed";
    public const string Rejected = "rejected";
    public const string Superseded = "superseded";

    private readonly SpecDialogProposalRepository _proposals = new(unitOfWork);
    private readonly SpecDialogProposalRows _rows = new(unitOfWork);

    private IQueryable<SpecDialogSession> Open(string platform, string threadId) =>
        unitOfWork.Set<SpecDialogSession>().Where(s => s.Platform == platform && s.ThreadId == threadId && s.IsOpen);

    /// <summary>The session id of the thread's newest open session; null when none is open.</summary>
    internal Task<string?> NewestAsync(string platform, string threadId, CancellationToken ct) =>
        Open(platform, threadId).OrderByDescending(s => s.Id).Select(s => (string?)s.SessionId).FirstOrDefaultAsync(ct);

    internal IQueryable<SpecDialogProposal> Rows(string sessionId) => _rows.Of(sessionId);

    /// <summary>The awaiting proposal's id (null when none awaits) and the card last posted to a chat thread.</summary>
    public async Task<(string? AwaitingId, string? PostedId, DateTimeOffset? PostedAt)> ReadAsync(
        string platform, string threadId, CancellationToken ct)
    {
        var row = await Open(platform, threadId).OrderByDescending(s => s.Id)
            .Select(s => new { s.SessionId, s.PostedProposalId, s.PostedAt }).FirstOrDefaultAsync(ct);
        return row is null ? (null, null, null) : (await _proposals.AwaitingAsync(row.SessionId, ct) is { } awaiting ? awaiting.Id : null,
            row.PostedProposalId, row.PostedAt);
    }

    /// <summary>The awaiting proposal under a fresh id, on a row of no run.</summary>
    public Task<(string Id, string? Replaced)> SetAwaitingAsync(
        string platform, string threadId, string json, CancellationToken ct) =>
        SetAwaitingAsync(platform, threadId, json, null, ct);

    /// <summary>Stores <paramref name="json"/> as run <paramref name="runId"/>'s proposal awaiting a decision under a fresh
    /// id, and returns that id with the id it superseded, if one was awaiting. Throws
    /// when no open session takes it, or when the run's proposal was already decided.</summary>
    public async Task<(string Id, string? Replaced)> SetAwaitingAsync(
        string platform, string threadId, string json, string? runId, CancellationToken ct)
    {
        var session = await NewestAsync(platform, threadId, ct)
            ?? throw new InvalidOperationException($"No open spec-dialog session on thread '{threadId}' takes the proposal.");
        var id = Guid.NewGuid().ToString("N");
        var key = runId ?? SpecDialogProposal.UnboundPrefix + id;
        await _proposals.EnsureAsync(session, key, json, ct);
        var (written, replaced) = await _rows.TakeAwaitingAsync(session, key, json, id, ct);
        if (written == 0) throw new InvalidOperationException($"The proposal '{key}' on thread '{threadId}' was already decided.");
        return (id, replaced);
    }

    /// <summary>Claims proposal <paramref name="id"/>: moves it from awaiting to filing and returns its JSON — or null when
    /// it is no longer the awaiting proposal. Release and clear of a filing are <see cref="SpecDialogConfirmedSlot"/>'s.</summary>
    public async Task<string?> ClaimAsync(string platform, string threadId, string id, CancellationToken ct)
    {
        if (await NewestAsync(platform, threadId, ct) is not { } session) return null;
        var target = Rows(session).Where(p => p.ProposalId == id && p.State == Awaiting);
        var json = await target.Select(p => p.ProposalJson).FirstOrDefaultAsync(ct);
        if (json is null) return null;
        return await SpecDialogProposalRows.MoveAsync(target, Filing, ct) == 1 ? json : null;
    }

    /// <summary>Rejects proposal <paramref name="id"/>; false when it is no longer awaiting.</summary>
    public async Task<bool> RejectAsync(string platform, string threadId, string id, CancellationToken ct) =>
        await NewestAsync(platform, threadId, ct) is { } session
        && await SpecDialogProposalRows.MoveAsync(Rows(session).Where(p => p.ProposalId == id && p.State == Awaiting), Rejected, ct) == 1;

    /// <summary>Supersedes whatever proposal is awaiting and returns its id, or null when none was.</summary>
    public async Task<string?> SupersedeAsync(string platform, string threadId, CancellationToken ct) =>
        await NewestAsync(platform, threadId, ct) is { } session ? await _rows.SupersedeAwaitingAsync(session, null, ct) : null;

    /// <summary>The awaiting proposal a chat thread was shown, written once its post succeeded, with the platform's time
    /// (null where it gives none); 2026-11-11-205fh: a failed post writes no id, so the thread binds no card.</summary>
    public async Task<int> SetPostedAsync(string platform, string threadId, string? id, DateTimeOffset? at, CancellationToken ct)
    {
        if (await NewestAsync(platform, threadId, ct) is not { } session) return 0;
        return await unitOfWork.Set<SpecDialogSession>().Where(s => s.SessionId == session).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.PostedProposalId, id).SetProperty(r => r.PostedAt, at), ct);
    }
}

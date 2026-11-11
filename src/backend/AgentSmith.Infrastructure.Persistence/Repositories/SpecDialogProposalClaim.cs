using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-10-ccd9: the awaiting proposal's state on the session row, changed only by compare-and-set
/// so two replicas or two surfaces cannot both decide one card. The awaiting write mirrors into
/// <c>LatestProposalJson</c> for builds that still read it; nothing else here touches that column
/// except to clear it with the awaiting one. Every write targets the thread's NEWEST open row by its Id,
/// the row every read here answers from, never every open row on the thread.
/// </summary>
public sealed class SpecDialogProposalClaim(IUnitOfWork unitOfWork)
{
    public const string Awaiting = "awaiting";
    public const string Filing = "filing";

    private IQueryable<SpecDialogSession> Open(string platform, string threadId) =>
        unitOfWork.Set<SpecDialogSession>().Where(s => s.Platform == platform && s.ThreadId == threadId && s.IsOpen);

    internal async Task<IQueryable<SpecDialogSession>> NewestAsync(string platform, string threadId, CancellationToken ct)
    {
        var id = await Open(platform, threadId).OrderByDescending(s => s.Id).Select(s => (long?)s.Id).FirstOrDefaultAsync(ct);
        return unitOfWork.Set<SpecDialogSession>().Where(s => id != null && s.Id == id);
    }

    /// <summary>The awaiting proposal's id (null when none awaits) and the card last posted to a chat thread.</summary>
    public async Task<(string? AwaitingId, string? PostedId, DateTimeOffset? PostedAt)> ReadAsync(
        string platform, string threadId, CancellationToken ct)
    {
        var row = await Open(platform, threadId).OrderByDescending(s => s.Id)
            .Select(s => new { s.ProposalId, s.ProposalState, s.PostedProposalId, s.PostedAt }).FirstOrDefaultAsync(ct);
        return row is null ? (null, null, null)
            : (row.ProposalState == Awaiting ? row.ProposalId : null, row.PostedProposalId, row.PostedAt);
    }

    /// <summary>Stores <paramref name="json"/> as the awaiting proposal under a fresh id and returns that id
    /// with the id it replaced, if one was awaiting. Throws when no open session takes it.</summary>
    public async Task<(string Id, string? Replaced)> SetAwaitingAsync(
        string platform, string threadId, string json, CancellationToken ct)
    {
        var newest = await NewestAsync(platform, threadId, ct);
        var replaced = await newest.Where(s => s.ProposalState == Awaiting).Select(s => s.ProposalId).FirstOrDefaultAsync(ct);
        var id = Guid.NewGuid().ToString("N");
        var rows = await newest.ExecuteUpdateAsync(s => s
            .SetProperty(r => r.AwaitingProposalJson, json).SetProperty(r => r.LatestProposalJson, json)
            .SetProperty(r => r.ProposalId, id).SetProperty(r => r.ProposalState, Awaiting)
            .SetProperty(r => r.ReviewingProposalJson, (string?)null), ct);
        if (rows == 0) throw new InvalidOperationException($"No open spec-dialog session on thread '{threadId}' takes the proposal.");
        return (id, replaced);
    }

    /// <summary>The draft under review; it never touches the awaiting proposal.</summary>
    public async Task<int> SetReviewingAsync(string platform, string threadId, string json, CancellationToken ct) =>
        await (await NewestAsync(platform, threadId, ct)).ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewingProposalJson, json), ct);

    /// <summary>Claims proposal <paramref name="id"/>: moves it into ConfirmedOutcomeJson under its id (205fe), frees the
    /// awaiting slot and returns its JSON — or null when it is no longer the awaiting proposal. Release and clear of the
    /// confirmed slot are <see cref="SpecDialogConfirmedSlot"/>'s.</summary>
    public async Task<string?> ClaimAsync(string platform, string threadId, string id, CancellationToken ct)
    {
        var target = (await NewestAsync(platform, threadId, ct))
            .Where(s => s.ProposalId == id && s.ProposalState == Awaiting && s.AwaitingProposalJson != null);
        var json = await target.Select(s => s.AwaitingProposalJson).FirstOrDefaultAsync(ct);
        if (json is null) return null;
        var rows = await target.ExecuteUpdateAsync(s => s
            .SetProperty(r => r.ConfirmedOutcomeJson, r => r.AwaitingProposalJson).SetProperty(r => r.ConfirmedProposalId, id)
            .SetProperty(r => r.AwaitingProposalJson, (string?)null).SetProperty(r => r.LatestProposalJson, (string?)null)
            .SetProperty(r => r.ProposalState, Filing), ct);
        return rows == 1 ? json : null;
    }

    /// <summary>Rejects proposal <paramref name="id"/>; false when it is no longer awaiting.</summary>
    public async Task<bool> RejectAsync(string platform, string threadId, string id, CancellationToken ct) =>
        await ClearAsync((await NewestAsync(platform, threadId, ct)).Where(s => s.ProposalId == id && s.ProposalState == Awaiting), ct) == 1;

    /// <summary>Clears whatever proposal is awaiting and returns its id, or null when none was.</summary>
    public async Task<string?> SupersedeAsync(string platform, string threadId, CancellationToken ct)
    {
        var newest = await NewestAsync(platform, threadId, ct);
        var id = await newest.Where(s => s.ProposalState == Awaiting).Select(s => s.ProposalId).FirstOrDefaultAsync(ct);
        return id is not null && await ClearAsync(newest.Where(s => s.ProposalId == id && s.ProposalState == Awaiting), ct) == 1 ? id : null;
    }

    /// <summary>The awaiting proposal a chat thread was shown, written once its post succeeded.</summary>
    public async Task<int> SetPostedAsync(string platform, string threadId, string id, DateTimeOffset at, CancellationToken ct) =>
        await (await NewestAsync(platform, threadId, ct)).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.PostedProposalId, id).SetProperty(r => r.PostedAt, at), ct);

    /// <summary>A row from before the deploy holds its proposal only in LatestProposalJson. It is given an id by
    /// compare-and-set — reviewing when <paramref name="underReview"/> says so, awaiting otherwise.</summary>
    public async Task BackfillAsync(string platform, string threadId, Func<string, bool> underReview, CancellationToken ct)
    {
        var pending = (await NewestAsync(platform, threadId, ct)).Where(s => s.ProposalId == null
            && s.AwaitingProposalJson == null && s.ReviewingProposalJson == null && s.LatestProposalJson != null);
        if (await pending.Select(s => s.LatestProposalJson).FirstOrDefaultAsync(ct) is not { } json) return;
        var id = Guid.NewGuid().ToString("N");
        if (underReview(json))
            await pending.ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewingProposalJson, json), ct);
        else
            await pending.ExecuteUpdateAsync(s => s.SetProperty(r => r.AwaitingProposalJson, json)
                .SetProperty(r => r.ProposalId, id).SetProperty(r => r.ProposalState, Awaiting), ct);
    }

    private static Task<int> ClearAsync(IQueryable<SpecDialogSession> rows, CancellationToken ct) =>
        rows.ExecuteUpdateAsync(s => s
            .SetProperty(r => r.AwaitingProposalJson, (string?)null).SetProperty(r => r.LatestProposalJson, (string?)null)
            .SetProperty(r => r.ProposalId, (string?)null).SetProperty(r => r.ProposalState, (string?)null), ct);
}

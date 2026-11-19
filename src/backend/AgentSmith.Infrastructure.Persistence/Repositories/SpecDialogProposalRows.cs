using System.Data.Common;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-a0b6d: the compare-and-set steps on one session's proposal rows that <see cref="SpecDialogProposalClaim"/>
/// and <see cref="SpecDialogConfirmedSlot"/> are made of. A step that writes JSON dates the row; a transition alone
/// leaves UpdatedAt as it was, because that time dates the turn's proposal in the view.
/// </summary>
internal sealed class SpecDialogProposalRows(IUnitOfWork unitOfWork)
{
    public IQueryable<SpecDialogProposal> Of(string sessionId) =>
        unitOfWork.Set<SpecDialogProposal>().Where(p => p.SessionId == sessionId);

    public static Task<int> MoveAsync(IQueryable<SpecDialogProposal> rows, string state, CancellationToken ct) =>
        rows.ExecuteUpdateAsync(s => s.SetProperty(p => p.State, state), ct);

    /// <summary>Supersedes every awaiting row but <paramref name="keep"/>'s, each by its id; the last id it moved.</summary>
    public async Task<string?> SupersedeAwaitingAsync(string session, string? keep, CancellationToken ct)
    {
        string? superseded = null;
        var awaiting = await Of(session).Where(p => p.State == SpecDialogProposalClaim.Awaiting && p.RunId != keep)
            .Select(p => p.ProposalId).ToListAsync(ct);
        foreach (var other in awaiting)
            if (await MoveAsync(Of(session).Where(p => p.ProposalId == other && p.State == SpecDialogProposalClaim.Awaiting),
                    SpecDialogProposalClaim.Superseded, ct) == 1)
                superseded = other;
        return superseded;
    }

    /// <summary>Supersedes what awaits and puts row <paramref name="key"/> up under <paramref name="id"/>; how many rows
    /// that wrote (none when the row was already decided) and the id it superseded.</summary>
    public async Task<(int Written, string? Replaced)> TakeAwaitingAsync(
        string session, string key, string json, string id, CancellationToken ct)
    {
        var replaced = await SupersedeAwaitingAsync(session, key, ct);
        try
        {
            return (await PutUpAsync(session, key, json, id, ct), replaced);
        }
        catch (DbException)
        {
            // Another replica put its proposal up between the two statements; the later one wins, as on one row it did.
            if (!await Of(session).AnyAsync(p => p.State == SpecDialogProposalClaim.Awaiting && p.RunId != key, ct)) throw;
            var winner = await SupersedeAwaitingAsync(session, key, ct);
            return (await PutUpAsync(session, key, json, id, ct), winner ?? replaced);
        }
    }

    private Task<int> PutUpAsync(string session, string key, string json, string id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return Of(session).Where(p => p.RunId == key && (p.State == null || p.State == SpecDialogProposalClaim.Awaiting))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProposalJson, json).SetProperty(p => p.ProposalId, id)
                .SetProperty(p => p.State, SpecDialogProposalClaim.Awaiting).SetProperty(p => p.UpdatedAt, now), ct);
    }
}

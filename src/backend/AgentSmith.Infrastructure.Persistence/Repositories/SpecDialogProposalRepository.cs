using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-a0b6d: a conversation's proposal rows (2026-11-10-d854c: one per run id). The writes here touch the JSON
/// only; a row's state changes through <see cref="SpecDialogProposalClaim"/> and <see cref="SpecDialogConfirmedSlot"/>,
/// each by compare-and-set.
/// </summary>
public sealed class SpecDialogProposalRepository(IUnitOfWork unitOfWork)
{
    /// <summary>Writes the run's row: replaces its JSON, or adds it. A write that loses the insert race to another writer
    /// of the same run updates the row that writer made.</summary>
    public async Task WriteAsync(string sessionId, string runId, string proposalJson, CancellationToken ct)
    {
        if (await UpdateAsync(runId, proposalJson, ct) > 0) return;
        if (!await AddAsync(sessionId, runId, proposalJson, ct)) await UpdateAsync(runId, proposalJson, ct);
    }

    /// <summary>Adds the run's row with no state unless it exists; the state is set by the caller's compare-and-set.</summary>
    internal async Task EnsureAsync(string sessionId, string runId, string proposalJson, CancellationToken ct)
    {
        if (!await unitOfWork.Set<SpecDialogProposal>().AnyAsync(p => p.RunId == runId, ct))
            await AddAsync(sessionId, runId, proposalJson, ct);
    }

    /// <summary>The conversation's turn proposals — rows a run wrote — with the time each was last written
    /// (2026-11-11-205ff) and its decision state.</summary>
    public async Task<IReadOnlyList<SpecDialogProposalRow>> ReadBySessionAsync(string sessionId, CancellationToken ct) =>
        (await unitOfWork.Set<SpecDialogProposal>().AsNoTracking()
            .Where(p => p.SessionId == sessionId && !p.RunId.StartsWith(SpecDialogProposal.UnboundPrefix))
            .Select(p => new { p.RunId, p.ProposalJson, p.UpdatedAt, p.CreatedAt, p.ProposalId, p.State })
            .ToListAsync(ct))
        .Select(p => new SpecDialogProposalRow(
            p.RunId, p.ProposalJson, p.UpdatedAt > p.CreatedAt ? p.UpdatedAt : p.CreatedAt, p.ProposalId, p.State))
        .ToList();

    /// <summary>The proposal awaiting a decision on the session, with its id; null when none awaits.</summary>
    public async Task<(string Id, string Json)?> AwaitingAsync(string sessionId, CancellationToken ct) =>
        await InState(sessionId, SpecDialogProposalClaim.Awaiting)
            .Select(p => new { p.ProposalId, p.ProposalJson }).FirstOrDefaultAsync(ct) is { ProposalId: { } id } row
            ? (id, row.ProposalJson) : null;

    /// <summary>The draft under review on the session; null when none is.</summary>
    public Task<string?> ReviewingAsync(string sessionId, CancellationToken ct) =>
        InState(sessionId, SpecDialogProposalClaim.Reviewing).Select(p => (string?)p.ProposalJson).FirstOrDefaultAsync(ct);

    /// <summary>The awaiting proposal of each of <paramref name="sessionIds"/> that has one, in one read.</summary>
    public async Task<IReadOnlyDictionary<string, string>> AwaitingBySessionsAsync(
        IReadOnlyCollection<string> sessionIds, CancellationToken ct) =>
        sessionIds.Count == 0
            ? new Dictionary<string, string>()
            : (await unitOfWork.Set<SpecDialogProposal>().AsNoTracking()
                .Where(p => sessionIds.Contains(p.SessionId) && p.State == SpecDialogProposalClaim.Awaiting)
                .Select(p => new { p.SessionId, p.ProposalJson }).ToListAsync(ct))
            .GroupBy(p => p.SessionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ProposalJson, StringComparer.Ordinal);

    public Task<int> DeleteBySessionAsync(string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogProposal>().Where(p => p.SessionId == sessionId).ExecuteDeleteAsync(ct);

    /// <summary>2026-11-13-3dc8: drops one run's row, written mid-turn for a draft the turn's reply removed.</summary>
    public Task<int> DeleteByRunAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogProposal>().Where(p => p.RunId == runId).ExecuteDeleteAsync(ct);

    private IQueryable<SpecDialogProposal> InState(string sessionId, string state) =>
        unitOfWork.Set<SpecDialogProposal>().AsNoTracking()
            .Where(p => p.SessionId == sessionId && p.State == state).OrderByDescending(p => p.Id);

    private async Task<bool> AddAsync(string sessionId, string runId, string proposalJson, CancellationToken ct)
    {
        var added = unitOfWork.Add(new SpecDialogProposal { SessionId = sessionId, RunId = runId, ProposalJson = proposalJson });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            added.State = EntityState.Detached; // the other writer's row stands; this one is not retried by a later save
            return false;
        }
    }

    private Task<int> UpdateAsync(string runId, string proposalJson, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return unitOfWork.Set<SpecDialogProposal>().Where(p => p.RunId == runId).ExecuteUpdateAsync(
            row => row.SetProperty(p => p.ProposalJson, proposalJson).SetProperty(p => p.UpdatedAt, now), ct);
    }
}

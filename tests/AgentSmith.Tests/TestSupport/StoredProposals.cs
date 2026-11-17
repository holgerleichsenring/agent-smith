using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-11-17-a0b6d: a test store's proposal rows, read untracked so a set-based write is always seen.</summary>
internal static class StoredProposals
{
    public static Task<List<SpecDialogProposal>> AllAsync(DbContext context) =>
        context.Set<SpecDialogProposal>().AsNoTracking().OrderBy(p => p.Id).ToListAsync();

    /// <summary>The newest row in <paramref name="state"/>, on any session; null when none is.</summary>
    public static Task<SpecDialogProposal?> InStateAsync(DbContext context, string state) =>
        context.Set<SpecDialogProposal>().AsNoTracking().Where(p => p.State == state).OrderByDescending(p => p.Id).FirstOrDefaultAsync();

    public static Task<SpecDialogProposal?> ByIdAsync(DbContext context, string proposalId) =>
        context.Set<SpecDialogProposal>().AsNoTracking().SingleOrDefaultAsync(p => p.ProposalId == proposalId);

    /// <summary>A row as a store holds it: a proposal of <paramref name="sessionId"/> in <paramref name="state"/>.</summary>
    public static SpecDialogProposal Row(string sessionId, string json, string? proposalId, string? state, string? runId = null) => new()
    {
        SessionId = sessionId, RunId = runId ?? SpecDialogProposal.UnboundPrefix + (proposalId ?? Guid.NewGuid().ToString("N")),
        ProposalJson = json, ProposalId = proposalId, State = state,
    };
}

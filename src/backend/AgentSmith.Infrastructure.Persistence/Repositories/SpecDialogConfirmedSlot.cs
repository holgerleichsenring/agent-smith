using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-11-205fe: the confirmed slot a claim writes — the proposal being filed, with its id. Only the claim writes it
/// (<see cref="SpecDialogProposalClaim.ClaimAsync"/>); a release and a clear act each in one statement and only on their
/// own proposal, so a filing that finishes after a newer proposal was claimed leaves that proposal's JSON alone.
/// One slot remains: a newer claim still replaces an older filing's durable copy (2026-11-10-825f).
/// </summary>
public sealed class SpecDialogConfirmedSlot(SpecDialogProposalClaim claim)
{
    /// <summary>Gives a claimed proposal that was refused back to the awaiting slot — only while it is still the one
    /// filing, its JSON is still confirmed and no newer proposal took the slot. False when any of that changed.</summary>
    public async Task<bool> ReleaseAsync(string platform, string threadId, string id, CancellationToken ct) =>
        await (await claim.NewestAsync(platform, threadId, ct))
            .Where(s => s.ProposalId == id && s.ProposalState == SpecDialogProposalClaim.Filing
                && s.AwaitingProposalJson == null && s.ConfirmedOutcomeJson != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.AwaitingProposalJson, r => r.ConfirmedOutcomeJson)
                .SetProperty(r => r.LatestProposalJson, r => r.ConfirmedOutcomeJson)
                .SetProperty(r => r.ConfirmedOutcomeJson, (string?)null).SetProperty(r => r.ConfirmedProposalId, (string?)null)
                .SetProperty(r => r.ProposalState, SpecDialogProposalClaim.Awaiting), ct) == 1;

    /// <summary>Clears the confirmed JSON after a full filing — only while it still belongs to <paramref name="id"/>.
    /// False when another proposal holds the slot, or none is named.</summary>
    public async Task<bool> ClearAsync(string platform, string threadId, string? id, CancellationToken ct) =>
        id is not null && await (await claim.NewestAsync(platform, threadId, ct))
            .Where(s => s.ConfirmedProposalId == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ConfirmedOutcomeJson, (string?)null)
                .SetProperty(r => r.ConfirmedProposalId, (string?)null), ct) == 1;
}

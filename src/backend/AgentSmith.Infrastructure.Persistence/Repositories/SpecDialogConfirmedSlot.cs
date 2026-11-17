using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-11-205fe: the proposal being filed, which a claim moved to filing
/// (<see cref="SpecDialogProposalClaim.ClaimAsync"/>). 2026-11-17-a0b6d: every claimed proposal is its own row, so a
/// filing that finishes after a newer proposal was claimed acts on its own row alone and leaves the newer one as it is.
/// A release and a clear are each one compare-and-set on that row.
/// </summary>
public sealed class SpecDialogConfirmedSlot(SpecDialogProposalClaim claim)
{
    /// <summary>Gives a claimed proposal that was refused back to awaiting — only while it is still filing and no newer
    /// proposal awaits. False when any of that changed.</summary>
    public async Task<bool> ReleaseAsync(string platform, string threadId, string id, CancellationToken ct)
    {
        if (await claim.NewestAsync(platform, threadId, ct) is not { } session) return false;
        if (await claim.Rows(session).AnyAsync(p => p.State == SpecDialogProposalClaim.Awaiting, ct)) return false;
        try
        {
            return await SpecDialogProposalRows.MoveAsync(claim.Rows(session)
                .Where(p => p.ProposalId == id && p.State == SpecDialogProposalClaim.Filing), SpecDialogProposalClaim.Awaiting, ct) == 1;
        }
        catch (DbException)
        {
            // A newer proposal was put up between the check and the move; the one-awaiting index kept it the only one.
            if (await claim.Rows(session).AnyAsync(p => p.State == SpecDialogProposalClaim.Awaiting, ct)) return false;
            throw;
        }
    }

    /// <summary>Marks the claimed proposal filed after a full filing — only while <paramref name="id"/> is still filing.
    /// False when it is not, or none is named.</summary>
    public async Task<bool> ClearAsync(string platform, string threadId, string? id, CancellationToken ct) =>
        id is not null && await claim.NewestAsync(platform, threadId, ct) is { } session
        && await SpecDialogProposalRows.MoveAsync(claim.Rows(session)
            .Where(p => p.ProposalId == id && p.State == SpecDialogProposalClaim.Filing), SpecDialogProposalClaim.Filed, ct) == 1;
}

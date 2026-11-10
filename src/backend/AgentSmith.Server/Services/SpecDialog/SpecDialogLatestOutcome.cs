using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What the session row holds for the pane: the proposal awaiting a decision and its id, the draft
/// under review (2026-11-10-ccd9, never approvable), and the latest filing.
/// </summary>
public sealed record SpecDialogLatestOutcome(
    OutcomeProposal? Proposal, SpecDialogFiling? Filing, string? ProposalId = null, OutcomeProposal? Reviewing = null)
{
    public static SpecDialogLatestOutcome None { get; } = new(null, null);
}

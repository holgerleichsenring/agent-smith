using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What a session durably holds for its proposal pane: the proposal under discussion and the
/// latest filing. Either may be absent — nothing proposed yet, a proposal rejected, nothing
/// filed yet.
/// </summary>
public sealed record SpecDialogLatestOutcome(OutcomeProposal? Proposal, SpecDialogFiling? Filing)
{
    public static SpecDialogLatestOutcome None { get; } = new(null, null);
}


using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-11-205fe: what a decision did, on the stored turn. The message was stored WITH its decision before the
/// gate; only a decision that decided its card keeps it, marked applied. Refused, Replaced and No take it back, so the
/// transcript never shows an approval that approved nothing — on the click path and the typed path alike.
/// </summary>
public sealed class SpecDialogDecisionOutcome(SpecDialogDecisionPath decisions, SpecDialogAnswerAdmission admission)
{
    /// <inheritdoc cref="SpecDialogDecisionPath.DecideAsync"/>
    public async Task<SpecDialogDecided> DecideAsync(
        ConversationState state, SpecDialogDecision decision, string? proposalId, bool mayStartRuns, CancellationToken ct,
        DateTimeOffset? sentAt = null)
    {
        var decided = await decisions.DecideAsync(state, decision, proposalId, mayStartRuns, ct, sentAt);
        if (decided == SpecDialogDecided.Decided) await admission.AppliedAsync(state, ct);
        else await admission.UndecideAsync(state, ct);
        return decided;
    }

    /// <summary>What routing reports for a decision: a decided card, or a handled message.</summary>
    public static SpecDialogRouted Routed(SpecDialogDecided decided) =>
        decided == SpecDialogDecided.Decided ? SpecDialogRouted.Decided : SpecDialogRouted.Handled;
}

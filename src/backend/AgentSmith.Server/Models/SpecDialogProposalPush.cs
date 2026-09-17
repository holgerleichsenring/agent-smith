using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-6d9c: the turn's typed outcome as the proposal pane reads it — the payload
/// of the hub's "SpecDialogProposal" push.
/// <para>
/// A PLAIN payload and deliberately not an event record: the hub-event type generator
/// scans records under the events namespace by base type and calls one without a matching
/// TypeScript interface drift. This derives from neither base, so it is not scanned —
/// and the check runs in CI rather than in the build the phase gate invokes, which is
/// what makes getting it wrong expensive.
/// </para>
/// <para>
/// An answer is never pushed. It proposes nothing, and a payload saying so would clear a
/// pane still showing the proposal under discussion.
/// </para>
/// </summary>
/// <param name="Kind">"bug", "phase" or "epic" — what the pane renders.</param>
/// <param name="Children">
/// An epic's children IN THE ORDER THEY WILL BE FILED. A pane listing them in another
/// order would show the operator a plan that is not the one about to be created.
/// </param>
public sealed record SpecDialogProposalPush(
    string DialogId,
    string Kind,
    SpecDialogBugView? Bug,
    SpecDialogPhaseView? Phase,
    SpecDialogPhaseView? Parent,
    IReadOnlyList<SpecDialogPhaseView> Children,
    DateTimeOffset At)
{
    /// <summary>
    /// 2026-09-17-042ed: what the turn's own review found against this proposal, each finding
    /// with the evidence line it rests on where it cites one. Empty is a clean review or one that
    /// could not be taken; the pane says nothing either way. Not a constructor parameter, so the
    /// composer's three shapes are unchanged and the dashboard type gains one optional field.
    /// </summary>
    public IReadOnlyList<ProposalFinding> Findings { get; init; } = [];
}

using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-6d9c: the turn's typed outcome as the proposal pane reads it — the payload
/// of the hub's "SpecDialogProposal" push.
/// <para>
/// A PLAIN payload and deliberately not an event record: the hub-event type generator
/// scans records under the events namespace by base type and calls one without a matching
/// TypeScript interface drift. This derives from neither base, so it is not scanned —
/// and the check runs in CI rather than in the build the spec gate invokes, which is
/// what makes getting it wrong expensive.
/// </para>
/// <para>An answer is never pushed: it proposes nothing, and a payload saying so would clear the pane.</para>
/// </summary>
/// <param name="Kind">"bug", "spec", "epic", "tickets" or "comment" — what the pane renders.</param>
/// <param name="Children">
/// An epic's children IN THE ORDER THEY WILL BE FILED. A pane listing them in another
/// order would show the operator a plan that is not the one about to be created.
/// </param>
public sealed record SpecDialogProposalPush(
    string DialogId,
    string Kind,
    SpecDialogBugView? Bug,
    SpecDialogDraftView? Spec,
    SpecDialogDraftView? Parent,
    IReadOnlyList<SpecDialogDraftView> Children,
    DateTimeOffset At)
{
    /// <summary>
    /// 2026-09-17-042ed: what the turn's own review found against this proposal, each finding
    /// with the evidence line it rests on where it cites one. Empty is a clean review or one that
    /// could not be taken; the pane says nothing either way. Not a constructor parameter, so the
    /// composer's three shapes are unchanged and the dashboard type gains one optional field.
    /// </summary>
    public IReadOnlyList<ProposalFinding> Findings { get; init; } = [];

    /// <summary>2026-10-13-8762i: every review round of the proposal's drafts.</summary>
    public IReadOnlyList<ProposalReviewRound> ReviewRounds { get; init; } = [];

    /// <summary>2026-10-13-8762i: the spec ids (or "series") not reviewed yet; null when all were.</summary>
    public IReadOnlyList<string>? NotReviewed { get; init; }

    /// <summary>2026-10-14-dbcek: a proposed list's ticket texts in the order they must merge; empty for every other kind.</summary>
    public IReadOnlyList<SpecDialogGraphTicketView> Tickets { get; init; } = [];

    /// <summary>2026-10-28-7a69c: the comment a ticket's conversation proposes to post; null for every other kind.</summary>
    public string? Comment { get; init; }
}

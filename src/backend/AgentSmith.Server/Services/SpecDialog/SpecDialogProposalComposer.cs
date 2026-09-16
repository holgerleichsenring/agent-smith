using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-6d9c: the turn's typed outcome, shaped for the proposal pane. Nothing here
/// re-words what the master wrote — the proposal is the part that is typed, and the reply
/// beside it is prose the transcript already carries.
/// </summary>
public sealed class SpecDialogProposalComposer(
    EpicChildOrderer orderer, BugTicketRenderer bugRenderer)
{
    public const string BugKind = "bug";
    public const string PhaseKind = "phase";
    public const string EpicKind = "epic";

    /// <summary>
    /// The pane payload, or null for an outcome that proposes nothing — an answer, which
    /// leaves the pane showing whatever is still under discussion.
    /// </summary>
    public SpecDialogProposalPush? Compose(
        string dialogId, OutcomeProposal proposal, DateTimeOffset at) => proposal switch
    {
        BugOutcome bug => new(
            dialogId, BugKind,
            new SpecDialogBugView(bug.Ticket.Title, bugRenderer.RenderBody(bug.Ticket)),
            Phase: null, Parent: null, Children: [], at),
        PhaseOutcome phase => new(
            dialogId, PhaseKind, Bug: null, View(phase.Draft),
            Parent: null, Children: [], at),
        EpicOutcome epic => new(
            dialogId, EpicKind, Bug: null, Phase: null, View(epic.Parent), Order(epic), at),
        _ => null,
    };

    /// <summary>
    /// The SAME orderer the filer runs, so the pane lists the children in the order they
    /// will be created. An epic whose edges cannot be ordered is handed back in the cut's
    /// own order — the filer refuses it, and showing the operator the shape they are about
    /// to be refused beats showing them nothing.
    /// </summary>
    private IReadOnlyList<SpecDialogPhaseView> Order(EpicOutcome epic) =>
        [.. orderer.Order(epic.Children).Children.Select(View)];

    private static SpecDialogPhaseView View(PhaseDraft draft) => new(
        draft.PhaseId, draft.Goal,
        [.. draft.Steps.Select(step => step.Action)],
        draft.Tests, draft.Done, draft.Requires);
}

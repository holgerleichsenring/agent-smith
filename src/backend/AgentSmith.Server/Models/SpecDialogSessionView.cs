namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: the conversation bound to one dialog id — what it is grounded in and
/// what has been said so far. The transcript is served because the dialog id survives a
/// reload: a page that kept the id and lost the conversation would look like a session
/// that had never happened.
/// </summary>
/// <param name="Transcript">
/// The turns as a person reads them: an assistant turn without its draft, which the proposal
/// pane shows; an operator turn exactly as written, pasted drafts included.
/// </param>
/// <param name="Proposal">The proposal under discussion, or null when none is — nothing
/// proposed yet, or the last one rejected.</param>
/// <param name="Filing">What the latest filing attempt created. Its moment tells a filing of
/// <paramref name="Proposal"/> from a filing of an earlier proposal.</param>
/// <param name="ProposalTurn">
/// The index in <paramref name="Transcript"/> of the turn the proposal card belongs on — the
/// last assistant turn that carried a draft. Null when there is no proposal.
/// </param>
public sealed record SpecDialogSessionView(
    string SessionId,
    SpecDialogProjectView Scope,
    IReadOnlyList<SpecDialogTurnView> Transcript,
    DateTimeOffset LastActivityAt,
    SpecDialogProposalPush? Proposal = null,
    SpecDialogFilingPush? Filing = null,
    int? ProposalTurn = null);

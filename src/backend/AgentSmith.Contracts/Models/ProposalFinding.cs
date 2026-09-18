namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-17-042ed: one thing a fresh instance found wrong with a proposal, reviewed inside the
/// design turn that produced it and put to the person who is asked to approve it.
/// <para>
/// It rides on the proposal, as the templates of 2026-09-13-ed5a do: the confirmation, the pane
/// push, the stored row and a reload all carry what the review said, and none of them has to be
/// told separately. The evidence line is resolved where the review is taken — the turn's look is
/// gone by the time anyone reads this, and an id nobody can resolve is not evidence.
/// </para>
/// </summary>
/// <param name="PhaseId">The phase of the proposal the finding is against.</param>
/// <param name="Problem">The verdict, in the reviewer's own vocabulary.</param>
/// <param name="Why">What is wrong with it.</param>
/// <param name="Quote">What the phase states, quoted by the reviewer; absent findings quote nothing.</param>
/// <param name="Evidence">
/// The framework-minted line of the look the finding rests on, resolved from the id it cited.
/// Null where the finding cites nothing — only a false premise does.
/// </param>
public sealed record ProposalFinding(
    string PhaseId,
    string Problem,
    string Why,
    string? Quote = null,
    string? Evidence = null);

using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the dashboard dialog surface's per-dialog read — the conversation on a
/// dialog id and what it is grounded in. This is the read the page issues after every message.
/// The caller's conversation list is <see cref="SpecDialogConversationList"/>, which since
/// 2026-09-17-042em the page may issue on the same message — but only while the row for the
/// conversation open in it is missing, untitled or behind, so the two are not paid together on
/// every reply.
/// <para>
/// 2026-11-10-ccd9: the approval card is built from the ROW — the proposal awaiting a decision and its
/// id — so it is there after a restart and on every replica, and it never expires. A draft under review
/// is returned beside it and never as the card. A row from before the deploy is given its id first.
/// </para>
/// </summary>
public sealed class SpecDialogViewReader(
    SpecDialogSessionManager sessions, SpecDialogProjectCatalog projects,
    SpecDialogPendingQuestions pendingQuestions, SpecDialogLatestOutcomeStore latestOutcome,
    SpecDialogProposalState proposals, SpecDialogChatCard cards,
    SpecDialogProposalComposer proposalComposer, SpecDialogTurnGate turns, SpecDialogHoldings holdings)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<SpecDialogView> ReadAsync(
        string dialogId, CancellationToken cancellationToken)
    {
        var state = await sessions.GetOpenByThreadAsync(Platform, dialogId, cancellationToken);
        if (state is null) return new SpecDialogView(dialogId, null, projects.All(), null, SpecDialogTurnLivenessView.Idle);
        await proposals.BackfillAsync(Platform, dialogId, cancellationToken);
        var latest = await latestOutcome.ReadAsync(Platform, dialogId, cancellationToken);
        var waiting = Waiting(dialogId, state);
        // 2026-10-08-e8b9g: images and sets with whether an approval cites them, and the bytes held.
        var held = await holdings.UploadsAsync(state, cancellationToken);
        var ticket = await holdings.TicketAsync(state, cancellationToken);
        return new SpecDialogView(
            dialogId,
            await holdings.WithAsync(Session(dialogId, state, latest, held.Images, ticket), state, held, cancellationToken),
            projects.All(),
            waiting ?? Approval(dialogId, state, latest),
            waiting is not null ? SpecDialogTurnLivenessView.Idle : turns.Liveness(state.JobId));
    }

    // A TURN WAITING ON ITS OWN QUESTION IS NOT COMPUTING. A turn blocked on a question waits for the
    // person, so it is reported idle beside the card asking them. An approval card blocks nothing.

    /// <summary>A question a running turn is blocked on, rebuilt for a page that has just loaded.</summary>
    private SpecDialogChannelQuestion? Waiting(string dialogId, ConversationState state) =>
        pendingQuestions.TryPeek(state.JobId, out var pending)
            ? SpecDialogChannelQuestion.From(dialogId, pending.Question, state.LastActivityAt, pending.ExpiresAt)
            : null;

    /// <summary>The approval card of the proposal awaiting a decision; its question id is the proposal id.</summary>
    private SpecDialogChannelQuestion? Approval(string dialogId, ConversationState state, SpecDialogLatestOutcome latest) =>
        latest is { ProposalId: { } id, Proposal: { } proposal }
            ? SpecDialogChannelQuestion.From(dialogId, cards.CardOf(state, proposal, id), state.LastActivityAt)
            : null;

    // 2026-11-09-9a98: a stored mark with no turn computing here was cancelled, failed or belongs to
    // another replica. Not reviewed is true in all three, so the row needs no cleanup on any of them.
    private OutcomeProposal Current(OutcomeProposal proposal, ConversationState state) =>
        proposal.UnderReview && !turns.Liveness(state.JobId).Computing ? proposal with { UnderReview = false } : proposal;

    private SpecDialogSessionView Session(
        string dialogId, ConversationState state, SpecDialogLatestOutcome latest,
        IReadOnlyList<SpecDialogImageView> images, SpecDialogTicketView? ticket)
    {
        var card = latest.Proposal is null ? null : SpecDialogShownTranscript.CardTurn(state.Transcript);
        // The proposal was made by the turn that carried it, so that turn's moment is its own.
        var at = card is { } turn ? state.Transcript[turn].At : state.LastActivityAt;
        return new(state.JobId,
            projects.Of(state.Project, state.Scope?.Repos ?? []),
            SpecDialogShownTranscript.Turns(state.Transcript),
            state.LastActivityAt,
            images,
            state.Subject,
            latest.Proposal is null ? null
                : proposalComposer.Compose(dialogId, Current(latest.Proposal, state), at) is { } push
                    ? push with { ProposalId = latest.ProposalId } : null,
            latest.Filing is null
                ? null
                : new SpecDialogFilingPush(dialogId, latest.Filing.Filed, latest.Filing.Error,
                    latest.Filing.At, latest.Filing.Notes ?? []),
            card,
            ticket)
        {
            Reviewing = latest.Reviewing is null ? null
                : proposalComposer.Compose(dialogId, Current(latest.Reviewing, state), state.LastActivityAt) is { } draft
                    ? draft with { Reviewing = true } : null,
        };
    }
}

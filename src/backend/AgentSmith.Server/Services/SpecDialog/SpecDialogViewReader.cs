using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the dashboard dialog surface's per-dialog read — the conversation on a
/// dialog id and what it is grounded in. This is the read the page issues after every message.
/// The caller's conversation list is <see cref="SpecDialogConversationList"/>, which since
/// 2026-09-17-042em the page may issue on the same message — but only while the row for the
/// conversation open in it is missing, untitled or behind, so the two are not paid together on
/// every reply.
/// </summary>
public sealed class SpecDialogViewReader(
    SpecDialogSessionManager sessions, SpecDialogProjectCatalog projects,
    SpecDialogPendingQuestions pendingQuestions, SpecDialogLatestOutcomeStore latestOutcome,
    SpecDialogProposalComposer proposalComposer, SpecDialogTurnGate turns,
    SpecDialogAttachmentRepository attachments)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<SpecDialogView> ReadAsync(
        string dialogId, CancellationToken cancellationToken)
    {
        var state = await sessions.GetOpenByThreadAsync(Platform, dialogId, cancellationToken);
        var latest = state is null
            ? SpecDialogLatestOutcome.None
            : await latestOutcome.ReadAsync(Platform, dialogId, cancellationToken);
        var asked = state is null ? null : Asked(dialogId, state);
        var images = state is null
            ? []
            : await Images(state.JobId, cancellationToken);
        return new SpecDialogView(
            dialogId,
            state is null ? null : Session(dialogId, state, latest, images),
            projects.All(),
            asked,
            state is null || asked is not null
                ? SpecDialogTurnLivenessView.Idle
                : turns.Liveness(state.JobId));
    }

    // A TURN WAITING ON ITS OWN QUESTION IS NOT COMPUTING. A design turn's ask_human blocks
    // inside the turn's execution and its wait has no deadline at all — it ends when the
    // person answers. Reported as computing, it would render a working line beside the very
    // card asking them to answer, so the question outranks the flag.

    /// <summary>
    /// What the turn is blocked on, rebuilt for a page that has just loaded. An expired wait
    /// is still reported rather than hidden: the card needs the deadline to say so, and a
    /// silently missing question is indistinguishable from a turn that never asked.
    /// </summary>
    private SpecDialogChannelQuestion? Asked(string dialogId, ConversationState state) =>
        pendingQuestions.TryPeek(state.JobId, out var pending)
            ? SpecDialogChannelQuestion.From(
                dialogId, pending.Question, state.LastActivityAt, pending.ExpiresAt)
            : null;

    /// <summary>
    /// 2026-09-20-3af8: the conversation's images, addressed rather than inlined — this read is
    /// issued after every message, so the bytes would be re-sent on every reply.
    /// </summary>
    private async Task<IReadOnlyList<SpecDialogImageView>> Images(
        string sessionId, CancellationToken cancellationToken) =>
        [.. (await attachments.ListAsync(sessionId, cancellationToken))
            .Select(row => new SpecDialogImageView(row.Id, row.MediaType, row.At))];

    private SpecDialogSessionView Session(
        string dialogId, ConversationState state, SpecDialogLatestOutcome latest,
        IReadOnlyList<SpecDialogImageView> images)
    {
        var card = latest.Proposal is null ? null : SpecDialogShownTranscript.CardTurn(state.Transcript);
        return new(state.JobId,
            projects.Of(state.Project, state.Scope?.Repos ?? []),
            SpecDialogShownTranscript.Turns(state.Transcript),
            state.LastActivityAt,
            images,
            state.Subject,
            latest.Proposal is null
                ? null
                // The proposal was made by the turn that carried it, so that turn's moment is its own.
                : proposalComposer.Compose(dialogId, latest.Proposal,
                    card is { } turn ? state.Transcript[turn].At : state.LastActivityAt),
            latest.Filing is null
                ? null
                : new SpecDialogFilingPush(dialogId, latest.Filing.Filed, latest.Filing.Error,
                    latest.Filing.At, latest.Filing.Notes ?? []),
            card);
    }
}

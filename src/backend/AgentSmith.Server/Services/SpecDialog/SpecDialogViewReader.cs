using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the dashboard dialog surface's per-dialog read — the conversation on a
/// dialog id and what it is grounded in. The caller's conversation list is
/// <see cref="SpecDialogConversationList"/>, off this read's every-message path.
/// </summary>
public sealed class SpecDialogViewReader(
    SpecDialogSessionManager sessions, SpecDialogProjectCatalog projects,
    SpecDialogPendingQuestions pendingQuestions, SpecDialogLatestOutcomeStore latestOutcome,
    SpecDialogProposalComposer proposalComposer)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<SpecDialogView> ReadAsync(
        string dialogId, CancellationToken cancellationToken)
    {
        var state = await sessions.GetOpenByThreadAsync(Platform, dialogId, cancellationToken);
        var latest = state is null
            ? SpecDialogLatestOutcome.None
            : await latestOutcome.ReadAsync(Platform, dialogId, cancellationToken);
        return new SpecDialogView(
            dialogId,
            state is null ? null : Session(dialogId, state, latest),
            projects.All(),
            state is null ? null : Asked(dialogId, state));
    }

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

    private SpecDialogSessionView Session(
        string dialogId, ConversationState state, SpecDialogLatestOutcome latest)
    {
        var card = latest.Proposal is null ? null : SpecDialogShownTranscript.CardTurn(state.Transcript);
        return new(state.JobId,
            projects.Of(state.Project, state.Scope?.Repos ?? []),
            SpecDialogShownTranscript.Turns(state.Transcript),
            state.LastActivityAt,
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

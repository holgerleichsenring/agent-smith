using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the dashboard dialog surface's one read — the conversation on a dialog
/// id, what it is grounded in, and what else this caller could resume.
/// <para>
/// The resumable list is the CALLER's own sessions, filtered where every caller passes:
/// 2026-09-15-9033 narrowed the manager's listing itself, because a session id is all
/// "/spec resume" needs and that command is reachable from any chat thread. A second filter
/// here would be a second answer to the same question.
/// </para>
/// </summary>
public sealed class SpecDialogViewReader(
    SpecDialogSessionManager sessions, SpecDialogProjectCatalog projects,
    SpecDialogPendingQuestions pendingQuestions)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<SpecDialogView> ReadAsync(
        string dialogId, string owner, CancellationToken cancellationToken)
    {
        var state = await sessions.GetOpenByThreadAsync(Platform, dialogId, cancellationToken);
        var open = await sessions.ListOpenAsync(owner, Platform, cancellationToken);
        return new SpecDialogView(
            dialogId,
            state is null ? null : Session(state),
            projects.All(),
            [.. open.Select(Summary)],
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

    private SpecDialogSessionView Session(ConversationState state) =>
        new(state.JobId,
            projects.Of(state.Project, state.Scope?.Repos ?? []),
            [.. state.Transcript.Select(turn => new SpecDialogTurnView(
                turn.Role.ToString().ToLowerInvariant(), turn.Text, turn.At))],
            state.LastActivityAt);

    private static SpecDialogSessionSummary Summary(ConversationState state) =>
        new(state.JobId, state.Project, state.Transcript.Count, state.LastActivityAt);
}

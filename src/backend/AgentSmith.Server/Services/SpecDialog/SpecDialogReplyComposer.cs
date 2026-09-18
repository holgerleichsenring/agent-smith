using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Builds the chat reply texts for the spec-dialog session flow. Pure text
/// composition — no I/O, no state.
/// <para>
/// 2026-09-15-cb3e: each line is composed for the channel that will read it. The dialect
/// arrives with the <see cref="SpecDialogMarkup"/> the messenger binds, so a line written
/// here cannot reach a browser as the asterisks and shortcodes a chat client would have
/// expanded.
/// </para>
/// <para>
/// 2026-09-17-042ek: the same binding decides the WORDING, through
/// <see cref="SpecDialogMarkup.Wording"/>. The dialog page has no thread and no slash
/// command, so EVERY sentence it can reach names neither — the two that answer a typed
/// command included. The page posts what the operator typed verbatim, so a person who
/// types "/spec list" or "/spec resume" into the box reaches those two replies, and
/// answering them by naming more commands is how a page teaches the wrong thing.
/// </para>
/// </summary>
public sealed class SpecDialogReplyComposer
{
    public ComposedReply ComposeOpened(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` opened — scope {m.Bold(state.Project)} " +
        $"({FormatRepos(state)}). Describe what you want to build; " +
        m.Wording(
            "`/spec new` forks this thread onto a fresh session.",
            "New conversation starts a separate spec dialog."));

    public ComposedReply ComposeResumed(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` resumed — scope {m.Bold(state.Project)}, " +
        $"{state.Transcript.Count} turn(s) so far. Continuing where we left off.");

    public ComposedReply ComposeAlreadyOpen(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` is already open " +
        m.Wording("on this thread ", "here ") +
        $"(scope {m.Bold(state.Project)}, {state.Transcript.Count} turn(s)). " +
        "Keep typing to continue, or " +
        m.Wording("fork with `/spec new`.", "start a separate spec dialog with New conversation."));

    public ComposedReply ComposeResumeUsage() => new(m =>
        m.Wording(
            "Usage: `/spec resume <id>` — see `/spec list` for open sessions.",
            "Nothing was named to resume. Open a conversation from the list on the left."));

    public ComposedReply ComposeResumeRefused(SpecDialogResumeRefused refused) => new(_ =>
        refused.QuestionPending
            ? $"Spec dialog `{refused.SessionId}` is waiting for an answer where it is open. " +
              "Answer it there, then open the conversation here."
            : $"Spec dialog `{refused.SessionId}` is in the middle of a turn. " +
              "Open it here once that turn has replied.");

    public ComposedReply ComposeSessionNotFound(string sessionId) => new(m =>
        $"No spec-dialog session `{sessionId}` found. " +
        m.Wording(
            "`/spec list` shows open sessions.",
            "Your conversations are listed on the left."));

    public ComposedReply ComposeList(IReadOnlyList<ConversationState> sessions) => new(m =>
    {
        if (sessions.Count == 0)
            return m.Wording(
                "No open spec-dialog sessions. Start one with `/spec`.",
                "No open spec-dialog sessions. Start one with New conversation.");

        var lines = sessions.Select(s =>
            $"- `{s.JobId}` — {m.Bold(s.Project)}, {s.Transcript.Count} turn(s), " +
            $"last activity {s.LastActivityAt:u}");
        return "Open spec-dialog sessions:\n" + string.Join("\n", lines);
    });

    // Reached from the page by New conversation with several projects and none picked. The
    // control that picks one is labelled Project and sits directly above that button.
    public ComposedReply ComposeChoiceRequired(IReadOnlyList<string> projects) => new(m =>
        "Multiple projects configured. Pick a scope: " +
        m.Wording(
            string.Join(", ", projects.Select(p => $"`/spec {p}`")),
            string.Join(", ", projects.Select(m.Bold))
                + " — choose one from the Project list and start the conversation again."));

    public ComposedReply ComposeUnknownProject(string requested, IReadOnlyList<string> projects) =>
        new(m => $"Unknown project {m.Bold(requested)}. Configured projects: " +
            string.Join(", ", projects.Select(m.Bold)));

    public ComposedReply ComposeTurnInProgress(ConversationState state) => new(m =>
        $"Still working on the previous turn (scope {m.Bold(state.Project)}). " +
        $"Your message is recorded in the transcript and will inform the next turn.");

    public ComposedReply ComposeTurnFailed(string reason) => new(_ =>
        $"The design turn failed: {reason}");

    public ComposedReply ComposeQuestion(string question) => new(m =>
        $"{m.Emoji("question", "❓")} {question}\n"
        + m.Italic(m.Wording(
            "Reply in this thread to answer.", "Write your answer below.")));

    private static string FormatRepos(ConversationState state) =>
        state.Scope is { Repos.Count: > 0 }
            ? string.Join(", ", state.Scope.Repos)
            : "no repos";
}

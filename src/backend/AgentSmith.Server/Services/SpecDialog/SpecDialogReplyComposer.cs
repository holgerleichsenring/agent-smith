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
/// command, so EVERY sentence it can reach names neither.
/// </para>
/// <para>
/// 2026-09-22-2a86: the lines that answered a typed list, resume or fork went with those
/// spellings, and the two that offered the fork stopped offering it — a sentence naming a
/// command the parser no longer reads is worse than no sentence at all. On chat a separate
/// conversation is a separate THREAD, which is what they say now.
/// </para>
/// </summary>
public sealed class SpecDialogReplyComposer
{
    public ComposedReply ComposeOpened(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` opened — scope {m.Bold(state.Project)} " +
        $"({FormatRepos(state)}). Describe what you want to build; " +
        m.Wording(
            "a new thread starts a separate spec dialog.",
            "New conversation starts a separate spec dialog."));

    public ComposedReply ComposeAlreadyOpen(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` is already open " +
        m.Wording("on this thread ", "here ") +
        $"(scope {m.Bold(state.Project)}, {state.Transcript.Count} turn(s)). " +
        "Keep typing to continue, or " +
        m.Wording(
            "start a separate spec dialog in a new thread.",
            "start a separate spec dialog with New conversation."));

    // Several projects configured and none named. The page's own control that picks one is
    // labelled Project and sits directly above the New conversation button.
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

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
/// </summary>
public sealed class SpecDialogReplyComposer
{
    public ComposedReply ComposeOpened(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` opened — scope {m.Bold(state.Project)} " +
        $"({FormatRepos(state)}). Describe what you want to build; " +
        $"`/spec new` forks this thread onto a fresh session.");

    public ComposedReply ComposeResumed(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` resumed — scope {m.Bold(state.Project)}, " +
        $"{state.Transcript.Count} turn(s) so far. Continuing where we left off.");

    public ComposedReply ComposeAlreadyOpen(ConversationState state) => new(m =>
        $"Spec dialog `{state.JobId}` is already open on this thread " +
        $"(scope {m.Bold(state.Project)}, {state.Transcript.Count} turn(s)). " +
        $"Keep typing to continue, or fork with `/spec new`.");

    public ComposedReply ComposeResumeUsage() => new(_ =>
        "Usage: `/spec resume <id>` — see `/spec list` for open sessions.");

    public ComposedReply ComposeSessionNotFound(string sessionId) => new(_ =>
        $"No spec-dialog session `{sessionId}` found. `/spec list` shows open sessions.");

    public ComposedReply ComposeList(IReadOnlyList<ConversationState> sessions) => new(m =>
    {
        if (sessions.Count == 0)
            return "No open spec-dialog sessions. Start one with `/spec`.";

        var lines = sessions.Select(s =>
            $"- `{s.JobId}` — {m.Bold(s.Project)}, {s.Transcript.Count} turn(s), " +
            $"last activity {s.LastActivityAt:u}");
        return "Open spec-dialog sessions:\n" + string.Join("\n", lines);
    });

    public ComposedReply ComposeChoiceRequired(IReadOnlyList<string> projects) => new(_ =>
        "Multiple projects configured. Pick a scope: " +
        string.Join(", ", projects.Select(p => $"`/spec {p}`")));

    public ComposedReply ComposeUnknownProject(string requested, IReadOnlyList<string> projects) =>
        new(m => $"Unknown project {m.Bold(requested)}. Configured projects: " +
            string.Join(", ", projects.Select(m.Bold)));

    public ComposedReply ComposeTurnInProgress(ConversationState state) => new(m =>
        $"Still working on the previous turn (scope {m.Bold(state.Project)}). " +
        $"Your message is recorded in the transcript and will inform the next turn.");

    public ComposedReply ComposeTurnFailed(string reason) => new(_ =>
        $"The design turn failed: {reason}");

    public ComposedReply ComposeQuestion(string question) => new(m =>
        $"{m.Emoji("question", "❓")} {question}\n{m.Italic("Reply in this thread to answer.")}");

    private static string FormatRepos(ConversationState state) =>
        state.Scope is { Repos.Count: > 0 }
            ? string.Join(", ", state.Scope.Repos)
            : "no repos";
}

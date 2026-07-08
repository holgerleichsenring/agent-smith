using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Builds the chat reply texts for the spec-dialog session flow. Pure text
/// composition — no I/O, no state.
/// </summary>
public sealed class SpecDialogReplyComposer
{
    public string ComposeOpened(ConversationState state) =>
        $"Spec dialog `{state.JobId}` opened — scope *{state.Project}* " +
        $"({FormatRepos(state)}). Describe what you want to build; " +
        $"`/spec new` forks this thread onto a fresh session.";

    public string ComposeResumed(ConversationState state) =>
        $"Spec dialog `{state.JobId}` resumed — scope *{state.Project}*, " +
        $"{state.Transcript.Count} turn(s) so far. Continuing where we left off.";

    public string ComposeAlreadyOpen(ConversationState state) =>
        $"Spec dialog `{state.JobId}` is already open on this thread " +
        $"(scope *{state.Project}*, {state.Transcript.Count} turn(s)). " +
        $"Keep typing to continue, or fork with `/spec new`.";

    public string ComposeResumeUsage() =>
        "Usage: `/spec resume <id>` — see `/spec list` for open sessions.";

    public string ComposeSessionNotFound(string sessionId) =>
        $"No spec-dialog session `{sessionId}` found. `/spec list` shows open sessions.";

    public string ComposeList(IReadOnlyList<ConversationState> sessions)
    {
        if (sessions.Count == 0)
            return "No open spec-dialog sessions. Start one with `/spec`.";

        var lines = sessions.Select(s =>
            $"- `{s.JobId}` — *{s.Project}*, {s.Transcript.Count} turn(s), " +
            $"last activity {s.LastActivityAt:u}");
        return "Open spec-dialog sessions:\n" + string.Join("\n", lines);
    }

    public string ComposeChoiceRequired(IReadOnlyList<string> projects) =>
        "Multiple projects configured. Pick a scope: " +
        string.Join(", ", projects.Select(p => $"`/spec {p}`"));

    public string ComposeUnknownProject(string requested, IReadOnlyList<string> projects) =>
        $"Unknown project *{requested}*. Configured projects: " +
        string.Join(", ", projects.Select(p => $"*{p}*"));

    public string ComposeTurnRecorded(ConversationState state) =>
        $"Noted (turn {state.Transcript.Count}, scope *{state.Project}*). " +
        $"The grounded design answer arrives with the design-partner master (p0315b).";

    private static string FormatRepos(ConversationState state) =>
        state.Scope is { Repos.Count: > 0 }
            ? string.Join(", ", state.Scope.Repos)
            : "no repos";
}

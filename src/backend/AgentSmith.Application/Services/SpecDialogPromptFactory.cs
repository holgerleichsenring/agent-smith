using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0315b: renders the spec-dialog transcript into the design-partner
/// master's user prompt. The reply contract is stated here (answer the last
/// user turn; the reply text is delivered verbatim to the chat thread) so the
/// SKILL.md stays about behaviour, not plumbing.
/// </summary>
public sealed class SpecDialogPromptFactory : ISpecDialogPromptFactory
{
    public string Build(PipelineContext pipeline)
    {
        var transcript = pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, out var t) && t is not null ? t : [];
        if (transcript.Count == 0)
            throw new InvalidOperationException(
                "Spec-dialog run has an empty transcript — the turn runner must seed "
                + $"ContextKeys.{nameof(ContextKeys.SpecDialogTranscript)} with at least the opening user turn.");

        return $"""
            ## Design conversation
            The transcript of this design thread so far, oldest first. Respond to the
            LAST user turn; earlier turns are context you already produced or received.

            {RenderTranscript(transcript)}

            ## Your reply
            Write the reply text now — it is delivered verbatim to the chat thread.
            Answer from the code map above when it suffices; read source files through
            your tools only when the question needs real file content. Draft a fenced
            ```yaml phase spec only when the conversation has converged on work that
            warrants a phase — otherwise reply with no artifact.
            """;
    }

    // p0315e: one nudge covers every typed terminal outcome — a phase draft
    // (```yaml) and a bug/epic payload (```outcome) fail the same gate.
    public string BuildOutcomeFixNudge(string originalUserPrompt, string validationError) =>
        "The terminal outcome in your previous reply failed schema validation and was "
        + $"NOT shown to the operator. Validation error: {validationError}\n"
        + "Fix exactly what the error names and reply again with the full corrected "
        + "block (plus at most a line of framing). Do not repeat the invalid output.\n\n"
        + "Original task:\n" + originalUserPrompt;

    private static string RenderTranscript(IReadOnlyList<SpecDialogTurn> transcript)
    {
        var sb = new StringBuilder();
        foreach (var turn in transcript)
            sb.AppendLine($"[{turn.Role}] {turn.Text}");
        return sb.ToString().TrimEnd();
    }
}

using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// The conversation's turns as a dialog turn's prompt carries them, oldest first, each tagged with its role and,
/// where recorded, its author. The design partner and the ticket steward read the same transcript, so one
/// rendering serves both.
/// </summary>
public static class DialogTranscriptSection
{
    /// <summary>The seeded transcript; a run without one is a composition fault, so it throws.</summary>
    public static IReadOnlyList<SpecDialogTurn> Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var transcript = pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, out var t) && t is not null ? t : [];
        if (transcript.Count == 0)
            throw new InvalidOperationException(
                "Spec-dialog run has an empty transcript — the turn runner must seed "
                + $"ContextKeys.{nameof(ContextKeys.SpecDialogTranscript)} with at least the opening user turn.");
        return transcript;
    }

    public static string Render(IReadOnlyList<SpecDialogTurn> transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        var sb = new StringBuilder();
        foreach (var turn in transcript)
            sb.AppendLine(turn.Author is { } author ? $"[{turn.Role}: {author}] {turn.Text}" : $"[{turn.Role}] {turn.Text}");
        return sb.ToString().TrimEnd();
    }
}

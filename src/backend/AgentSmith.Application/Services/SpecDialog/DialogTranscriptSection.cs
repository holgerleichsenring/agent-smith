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

    /// <summary>The transcript as the prompt carries it: whole up to the bound, windowed past it.</summary>
    public static string Render(IReadOnlyList<SpecDialogTurn> transcript) => TranscriptWindow.Render(transcript);
}

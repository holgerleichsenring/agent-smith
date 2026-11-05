using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// The conversation's turns as a dialog turn's prompt carries them, oldest first, each tagged with its role and,
/// where recorded, its author. The design partner and the ticket steward read the same transcript, so one
/// rendering serves both. A turn that continues its master's thread already holds the earlier turns as messages.
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

    private const string SeededNote =
        "[Your earlier turns — what you read, called and answered — are the messages before this one. These turns "
        + "were added while or since you last answered; one may repeat an answer you already received. The contract "
        + "below is this turn's.]";

    /// <summary>The transcript as the prompt carries it. With a thread seeded for this master, only the turns it has
    /// not seen, under a note; otherwise whole up to the bound, windowed past it.</summary>
    public static string Render(PipelineContext pipeline, IReadOnlyList<SpecDialogTurn> transcript, bool stewarded) =>
        DialogThreadSeed.For(pipeline, stewarded) is { } thread
            ? SeededNote + Environment.NewLine + string.Join(Environment.NewLine, thread.Unseen.Select(TranscriptWindow.Line))
            : TranscriptWindow.Render(transcript);
}

using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: the operator's own messages of a design turn's transcript
/// (<see cref="ContextKeys.SpecDialogTranscript"/>), oldest first — never the design partner's turns.
/// </summary>
internal static class ProposalOperatorTurns
{
    public static IReadOnlyList<string> Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, out var transcript)
            && transcript is not null
            ? [.. transcript.Where(t => t.Role == SpecDialogTurn.UserRole).Select(t => t.Text)]
            : [];
    }
}

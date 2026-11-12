using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfd: the operator's open questions a design turn currently lays on its draft — the turn's
/// <see cref="SpecDialogReplySlot"/>, which state_open_questions writes. Its reviewers read them.
/// </summary>
internal static class ProposalOpenQuestions
{
    public static IReadOnlyList<OpenQuestion> Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
            ? slot.Questions
            : [];
    }
}

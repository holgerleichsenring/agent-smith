using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfd: the operator's open questions a design turn currently lays on its draft — the turn's
/// <see cref="SpecDialogReplySlot"/>, which state_open_questions replaces on every call, a re-prompt's included.
/// Read each round, so questions a revision stated reach the next round's reviewers. 2026-11-11-2bdfc: put back
/// when the re-prompt that stated them is not used, so the published questions are the ones stated with its drafts.
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

    /// <summary>Puts <paramref name="questions"/> back on the turn's slot; nothing without a slot.</summary>
    public static void Restore(PipelineContext pipeline, IReadOnlyList<OpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(questions);
        if (pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null)
            slot.Questions = questions;
    }
}

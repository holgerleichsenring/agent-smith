using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-6ad7: what a run does when the specification it was filed from is not on its
/// branch — it PARKS. The hand-back goes on the context and SpecHandback, which runs directly
/// after DeriveSpec, comments the ticket and moves it where a person can answer.
/// <para>
/// A failed step would finalize the ticket into the failure status instead, taking it out of the
/// open set because a file is not on a branch yet. The step therefore succeeds: the park is the
/// outcome, and the run short-circuits on the awaiting-answer flag the hand-back sets.
/// </para>
/// </summary>
public static class MissingSpecPark
{
    public static CommandResult Apply(PipelineContext pipeline, SpecHandback handback)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(handback);
        pipeline.Set(ContextKeys.SpecHandback, handback);
        return CommandResult.Ok($"awaiting_user_input: {handback.Reason}");
    }
}

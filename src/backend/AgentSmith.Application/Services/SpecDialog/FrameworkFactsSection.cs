using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-28-1da5e: what this framework would do with this ticket, rendered for the turn.
/// <para>
/// Only what is ABOUT THIS CASE and only what the model could not work out. The prompt is re-sent
/// every turn, so every sentence is paid for by the length of the conversation: a branch name it
/// will otherwise guess at earns its place, and a tour of the framework does not.
/// </para>
/// <para>
/// A conversation belonging to no ticket renders nothing, as the seeded ticket and the ticket read
/// already do.
/// </para>
/// </summary>
public static class FrameworkFactsSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<FrameworkFacts>(ContextKeys.SpecDialogFrameworkFacts, out var facts)
            || facts is null)
            return string.Empty;

        return $"""


            ## What this framework would do with this ticket

            These are its OWN values for this ticket, not a convention to apply yourself:

            - Work on it happens on the branch `{facts.WorkBranch}`.
            - It marks the ticket `{facts.EnqueuedLabel}` when it takes it up and
              `{facts.InProgressLabel}` while it is working.
            - A ticket it filed from an approved specification carries `{facts.ApprovedSetStamp}`.
            """;
    }
}

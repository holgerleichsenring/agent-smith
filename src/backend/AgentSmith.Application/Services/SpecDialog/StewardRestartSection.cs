using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-18-d6d2b: how THIS ticket starts again, for the ticket steward only — where a failed attempt leaves it (the
/// project's failed_status, else its own status, which the claim door holds), that the poll does not start it again,
/// and who starts the next attempt and how. Taken from the facts the server seeded and the start port it granted, so
/// the steward never tells a person their reply starts a run when this turn cannot start one.
/// </summary>
public static class StewardRestartSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<FrameworkFacts>(ContextKeys.SpecDialogFrameworkFacts, out var facts) || facts is null)
            return string.Empty;
        var failure = facts.FailedStatus is { } failed
            ? $"A failed attempt moves this ticket to `{failed}`, so the poll does not start it again by itself."
            : "A failed attempt leaves this ticket in its tracker status and the claim door holds it, so the poll does not start it again by itself.";
        var start = pipeline.TryGet<IAttemptStart>(ContextKeys.SpecDialogAttemptStart, out var arm) && arm is not null
            ? "When the person asks for the next attempt, start it with start_next_attempt. Its reason is only logged: the "
              + "attempt works the series, so a changed instruction goes into the spec with edit_series first."
            : "This person may not start runs; someone holding runs.control starts the next attempt with Retry or a reply here.";
        return $"""


            ## How this ticket starts again

            - {failure}
            - {start}
            """;
    }
}

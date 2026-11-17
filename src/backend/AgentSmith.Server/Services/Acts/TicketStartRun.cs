using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Acts;

/// <summary>
/// 2026-11-17-e061b: starts an existing ticket through a 'start' run — the server decided it may start; the run's step
/// tags and moves it under a run id. Answers the step's start, or null with how the run ended when it ended first.
/// </summary>
public sealed class TicketStartRun(IActRun runs)
{
    public async Task<(FiledWorkStart? Start, string? Ended)> StartAsync(ResolvedProject project, string ticketId, bool mayStartRuns)
    {
        ArgumentNullException.ThrowIfNull(project);
        var slot = new ActSlot<FiledWorkStart>();
        var result = await runs.RunAsync(project.Name, PipelinePresets.StartName, new Dictionary<string, object>
        {
            [ContextKeys.StartTicketRequest] = new StartTicketRequest(ticketId, mayStartRuns),
            [ContextKeys.ActOutcome] = slot,
        });
        return slot.Value is { } start ? (start, null) : (null, result.Message ?? "the start run ended before its step");
    }
}

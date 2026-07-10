using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Input to TicketClaimService.ClaimAsync. Built by webhook handlers and (future) pollers.
/// </summary>
public sealed record ClaimRequest(
    string Platform,
    string ProjectName,
    TicketId TicketId,
    string PipelineName,
    Dictionary<string, object>? InitialContext = null,
    Dictionary<string, string>? PlanAnswers = null,
    // p0320c: the run id reserved by the capacity queue's "queued" Run row. When
    // set, the claim threads it into the PipelineRequest so the launched run
    // REUSES that row (queued row becomes the running row) instead of creating a
    // new one per attempt. Null = a fresh run id is generated at execute time.
    string? ExistingRunId = null);

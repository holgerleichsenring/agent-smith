using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Explicit request to execute a pipeline. No regex parsing, no guessing.
/// Built by CLI commands, Slack intent parser, or TicketClaimService.
/// </summary>
public sealed record PipelineRequest(
    string ProjectName,
    string PipelineName,
    TicketId? TicketId = null,
    // 2026-11-17-713ea: IsInit is gone — a run is an init when its pipeline is init-project
    // (InitRunSeed), so no door can start one that is not. A stored request that still
    // carries the member deserializes; the member is ignored.
    bool Headless = false,
    Dictionary<string, object>? Context = null,
    Dictionary<string, string>? PlanAnswers = null,
    // p0281d: a CLI scan (api-scan / security-scan) sets this from `--agent` to run
    // WITHOUT a project entry — an ephemeral project is built from this agent + the
    // --source-path. When set it takes precedence over ProjectName. Null = the legacy
    // path (resolve ProjectName against the projects: catalog).
    string? AgentName = null,
    // p0320c: pre-reserved run id from the capacity queue's "queued" Run row.
    // When set, ExecutePipelineUseCase uses it instead of generating a fresh id,
    // so the queued row is upserted to "running" rather than duplicated.
    string? RunId = null,
    // p0326: inline ticket payload (the demo's trackerless path). When set,
    // FetchTicket materializes it instead of a provider lookup; TicketId stays
    // null — an inline run holds no lease and finalizes no tracker state.
    InlineTicket? InlineTicket = null);

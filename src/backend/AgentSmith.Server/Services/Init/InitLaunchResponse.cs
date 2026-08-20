namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: the wire shape of POST /api/projects/{name}/init. A started launch
/// carries the run id the dashboard links to; an already-running launch carries
/// the LIVE run's id AND the reason; the remaining refusals carry only a reason.
/// </summary>
public sealed record InitLaunchResponse(string? RunId, string? Reason);

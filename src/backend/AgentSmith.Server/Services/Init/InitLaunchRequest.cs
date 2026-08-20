namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0490: the body of POST /api/projects/{name}/init — what the operator ticked on the
/// init they are starting. Auto-accept rides the LAUNCH, not project configuration: a
/// per-project setting would silently apply to whatever opens a pull request next,
/// while consent belongs to the click that started THIS run.
/// <para>
/// A request that says nothing does not auto-accept. The dashboard's checkbox defaults
/// to ON and states its choice explicitly; a caller that omits the field never merges
/// anything by accident.
/// </para>
/// </summary>
public sealed record InitLaunchRequest(bool AutoCompletePullRequests = false);

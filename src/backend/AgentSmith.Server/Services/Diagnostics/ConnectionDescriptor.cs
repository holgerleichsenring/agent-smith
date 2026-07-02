namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// A probeable catalog connection as listed on page load — identity only, no
/// probe result. Probing is on demand (the operator clicks Test), so the initial
/// snapshot makes no outbound calls. <see cref="Kind"/> is "repo" or "tracker".
/// </summary>
public sealed record ConnectionDescriptor(string Name, string Type, string Kind);

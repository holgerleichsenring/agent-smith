namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// A probeable connection as listed on page load — identity only, no probe result
/// (probing is on demand). <see cref="Kind"/> is the specific type (repo / tracker /
/// agent / redis / persistence / sandbox / chat); <see cref="Category"/> groups them
/// on the page (service / agent / infra / chat).
/// </summary>
public sealed record ConnectionDescriptor(string Name, string Type, string Kind, string Category);

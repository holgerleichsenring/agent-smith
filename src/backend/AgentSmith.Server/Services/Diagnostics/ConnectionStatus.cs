namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Result of a read-only connectivity probe for one catalog connection.
/// <see cref="Kind"/> is "repo" or "tracker"; <see cref="Error"/> is a short,
/// secret-free reason when <see cref="Ok"/> is false.
/// </summary>
public sealed record ConnectionStatus(
    string Name, string Type, string Kind, bool Ok, long LatencyMs, string? Error);

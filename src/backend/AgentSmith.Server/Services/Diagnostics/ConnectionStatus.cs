namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Result of a read-only connectivity probe for one connection. <see cref="Kind"/>
/// is the specific type and <see cref="Category"/> its page group (service / agent /
/// infra / chat); <see cref="Error"/> is a short, secret-free reason when
/// <see cref="Ok"/> is false.
/// </summary>
public sealed record ConnectionStatus(
    string Name, string Type, string Kind, string Category, bool Ok, long LatencyMs, string? Error);

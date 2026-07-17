namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one agent catalog entry. <see cref="Models"/> is a
/// role→model map (coding, scan, …) so the UI can pick per-task models without
/// binding to the full <c>ModelRegistryConfig</c> shape. <see cref="KeySecret"/>
/// carries the env-NAME of the provider key — never a value.
/// </summary>
public sealed record AgentEntity(
    string Id,
    string Provider,
    IReadOnlyDictionary<string, string> Models,
    string? KeySecret)
{
    public AgentEntity() : this(string.Empty, string.Empty, new Dictionary<string, string>(), null) { }
}

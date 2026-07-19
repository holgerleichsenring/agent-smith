namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0349: one current config document row read from the entity-document store —
/// the type in the taxonomy, its id ('default' for singletons), the opaque JSON
/// doc, and the optimistic-concurrency version.
/// </summary>
public sealed record ConfigDocRow(string Type, string Id, string Doc, int Version);

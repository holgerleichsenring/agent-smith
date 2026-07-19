namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0349: one outgoing reference-graph edge from the entity being written to a
/// target entity (type + id). The source is the owning <see cref="ConfigDocWrite"/>.
/// </summary>
public sealed record ConfigDocEdge(string ToType, string ToId);

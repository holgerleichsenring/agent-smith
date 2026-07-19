using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>p0349: one config entity split out of the raw model — its type, id, JSON doc, and edges.</summary>
public sealed record DecomposedConfigDoc(
    string Type,
    string Id,
    string Doc,
    IReadOnlyList<ConfigDocEdge> Edges);

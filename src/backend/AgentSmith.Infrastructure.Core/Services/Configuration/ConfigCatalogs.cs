using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: the four name-keyed catalogs a project resolves its references against, as one
/// value. Every one of them is keyed by <see cref="ConfigNames.Comparer"/>, and passing
/// them together is what keeps that true — a builder handed the set cannot be handed three
/// case-insensitive catalogs and one ordinal one.
/// </summary>
public sealed record ConfigCatalogs(
    Dictionary<string, AgentConfig> Agents,
    Dictionary<string, RepoConnection> Repos,
    Dictionary<string, TrackerConnection> Trackers,
    Dictionary<string, ResolvedConnection> Connections);

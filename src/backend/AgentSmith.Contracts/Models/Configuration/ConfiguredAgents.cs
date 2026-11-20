namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-20-24bd: the agent catalog as the configuration holds it NOW — the server follows its store, so a limit
/// raised in the studio reaches a run already waiting for its model place.
/// </summary>
public sealed record ConfiguredAgents(IReadOnlyCollection<AgentConfig> Agents)
{
    /// <summary>Every agent <paramref name="config"/> names: the catalog, each project's and each pipeline override.</summary>
    public static ConfiguredAgents Of(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new([.. config.Agents.Values, .. config.Projects.Values.Select(p => p.Agent),
            .. config.Projects.Values.SelectMany(p => p.Pipelines).Select(p => p.Agent).OfType<AgentConfig>()]);
    }

    /// <summary>The limit the configuration states for <paramref name="key"/> now: the lowest any agent on that
    /// deployment sets; null when agents on it are configured and none sets one (the limit was lifted);
    /// <paramref name="fallback"/> when no configured agent is on that deployment.</summary>
    public int? LimitFor(string key, int? fallback)
    {
        var onKey = Agents.Where(a => ModelRunSlotKey.For(a) == key).ToList();
        if (onKey.Count == 0) return fallback;
        var limits = onKey.Where(a => a.MaxConcurrentRuns is not null).Select(a => a.MaxConcurrentRuns!.Value).ToList();
        return limits.Count > 0 ? limits.Min() : null;
    }
}

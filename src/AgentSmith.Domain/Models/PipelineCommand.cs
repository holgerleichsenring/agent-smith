namespace AgentSmith.Domain.Models;

/// <summary>
/// A typed pipeline command with optional parameters.
/// Replaces string-encoded commands like "SkillRoundCommand:architect:1".
/// </summary>
public sealed record PipelineCommand
{
    public string Name { get; }
    public string? SkillName { get; init; }
    public int? Round { get; init; }
    /// <summary>p0158g: names which configured repo this round operates on.
    /// Used by BootstrapDispatch to fan out one round per repo (different
    /// languages → different bootstrap skills, each scoped to its repo's
    /// sandbox). null on single-repo or repo-agnostic rounds.</summary>
    public string? RepoName { get; init; }

    /// <summary>p0161d: names which discovered context (sub-tree under
    /// <c>.agentsmith/contexts/</c>) this round writes into. Set by
    /// BootstrapDispatch when fanning out per (repo, component). null on
    /// pre-p0161d single-context rounds.</summary>
    public string? ContextName { get; init; }

    /// <summary>p0161d: repo-relative workdir for the component this round
    /// operates on. Mirrors RemoteContextDiscovery.Workdir. "." for
    /// single-component repos; sub-tree path (e.g. "server/") for
    /// monorepo components.</summary>
    public string? Workdir { get; init; }

    public PipelineCommand(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a parameterized skill round command. Optional repoName scopes
    /// the round to one configured repo (p0158g multi-repo dispatch). p0161d:
    /// optional contextName + workdir scope the round to one discovered
    /// component within that repo.
    /// </summary>
    public static PipelineCommand SkillRound(
        string commandName, string skillName, int round,
        string? repoName = null, string? contextName = null, string? workdir = null) =>
        new(commandName)
        {
            SkillName = skillName,
            Round = round,
            RepoName = repoName,
            ContextName = contextName,
            Workdir = workdir,
        };

    /// <summary>
    /// Creates a simple command without parameters.
    /// </summary>
    public static PipelineCommand Simple(string commandName) => new(commandName);

    /// <summary>
    /// Parses a legacy string-encoded command for backward compatibility.
    /// "SkillRoundCommand:architect:1" → PipelineCommand with SkillName and Round.
    /// </summary>
    public static PipelineCommand Parse(string encoded)
    {
        var parts = encoded.Split(':');
        if (parts.Length >= 3 && int.TryParse(parts[2], out var round))
            return new PipelineCommand(parts[0]) { SkillName = parts[1], Round = round };
        if (parts.Length == 2)
            return new PipelineCommand(parts[0]) { SkillName = parts[1] };
        return new PipelineCommand(encoded);
    }

    public string DisplayName => SkillName is not null
        ? $"{Name}:{SkillName}{(Round.HasValue ? $":{Round}" : "")}"
        : Name;

    public override string ToString() => DisplayName;
}

using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for discovering skill candidates from configured sources.
/// </summary>
public sealed record DiscoverSkillsContext(
    string SkillSourcesPath,
    IReadOnlyList<string> InstalledSkillNames,
    PipelineContext Pipeline) : ICommandContext;

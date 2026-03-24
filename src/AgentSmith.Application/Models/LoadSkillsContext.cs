using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading skill definitions from the configured skills path.
/// Used by repo-less pipelines that don't run BootstrapProject.
/// </summary>
public sealed record LoadSkillsContext(
    string SkillsPath,
    PipelineContext Pipeline) : ICommandContext;

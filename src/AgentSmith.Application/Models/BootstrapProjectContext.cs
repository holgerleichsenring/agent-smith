using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for auto-bootstrapping a repository with a .context.yaml file.
/// Detects project type and generates CCS if not already present.
/// Carries AgentConfig for per-project LLM client creation.
/// </summary>
public sealed record BootstrapProjectContext(
    Repository Repository,
    AgentConfig Agent,
    string SkillsPath,
    PipelineContext Pipeline) : ICommandContext;

using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the producer-loop runtime that drives the matched bootstrap skill
/// (csharp/node/python/generic-bootstrap). The handler resolves the skill from
/// AvailableRoles by name, builds a tool-bearing chat call exposing the bootstrap
/// WriteFile subset (path-write-guard restricted to .agentsmith/context.yaml +
/// coding-principles.md), and persists the skill's Markdown summary into
/// SkillOutputs for downstream WriteRunResult capture.
/// </summary>
public sealed record BootstrapRoundContext(
    string SkillName,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;

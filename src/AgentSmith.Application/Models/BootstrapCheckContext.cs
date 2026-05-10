using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the BootstrapCheck step. The handler resolves the active sandbox
/// from <see cref="PipelineContext"/> and probes for the standard meta-files
/// (<c>.agentsmith/context.yaml</c>, <c>.agentsmith/coding-principles.md</c>),
/// publishing <c>context_yaml_present</c> and <c>coding_principles_present</c>.
/// </summary>
public sealed record BootstrapCheckContext(PipelineContext Pipeline) : ICommandContext;

using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the BootstrapGate step. The handler reads concept values
/// (<c>context_yaml_present</c>, <c>coding_principles_present</c>, and for
/// api-security-scan also <c>source_available</c>) and decides whether to abort
/// the pipeline. No payload is needed beyond the pipeline reference.
/// </summary>
public sealed record BootstrapGateContext(PipelineContext Pipeline) : ICommandContext;

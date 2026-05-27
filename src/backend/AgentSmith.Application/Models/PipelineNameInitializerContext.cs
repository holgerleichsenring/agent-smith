using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PipelineNameInitializer step. The handler reads the resolved
/// pipeline name from <see cref="PipelineContext"/> (populated by
/// <c>PipelineConfigResolver</c>), so no payload is needed beyond the pipeline reference.
/// </summary>
public sealed record PipelineNameInitializerContext(PipelineContext Pipeline) : ICommandContext;

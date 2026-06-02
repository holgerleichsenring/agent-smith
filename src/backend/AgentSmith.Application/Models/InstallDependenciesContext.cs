using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0202: run each context's <c>ci.install_command</c> inside its sandbox so
/// non-dotnet test runners (jest, pytest, mvn, cargo, go) find their
/// dependencies installed before the Test step. The handler reads the
/// per-context ProjectMaps and sandboxes straight from the pipeline context.
/// </summary>
public sealed record InstallDependenciesContext(PipelineContext Pipeline) : ICommandContext;

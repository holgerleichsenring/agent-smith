using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// 2026-09-22-6c46: the cpu/memory a project would be given if it declared no resources of
/// its own, together with the LAYER that produced it. 2026-10-12-006d: a project's runs take
/// their pipeline per run (a ticket's labels, a trigger's default, the CLI), so no single
/// answer is true for the project — <see cref="Values"/> and <see cref="Layer"/> are what a
/// run that changes code gets, <see cref="NoCodeValues"/> what a run that changes none is held
/// to. The repository's context document can still pre-empt the code-run answer per run.
/// </summary>
public sealed record InheritedSandboxResources(
    ResourceLimits Values, SandboxResourceLayer Layer, ResourceLimits NoCodeValues);

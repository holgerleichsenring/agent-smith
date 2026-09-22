using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// 2026-09-22-6c46: the cpu/memory a project would be given if it declared no resources of
/// its own, together with the LAYER that produced it. The layer is carried because the
/// answer is not one value: a pipeline that changes no code is held to the light profile
/// whatever the global default says, and the repository's context document can pre-empt the
/// global default per run without anything at config time knowing it will.
/// </summary>
public sealed record InheritedSandboxResources(ResourceLimits Values, SandboxResourceLayer Layer);

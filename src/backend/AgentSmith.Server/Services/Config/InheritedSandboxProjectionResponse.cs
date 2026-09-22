namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-22-6968: what a project's five scalar sandbox controls would inherit if the
/// project declared nothing, projected for the wire. Each value carries its provenance the
/// way <see cref="ConfigResolvedSettings"/> does — a run-resolved value (the toolchain
/// image) has a NULL value, and the form must say "detected per run" rather than draw a
/// blank, because a blank there reads as "no image", which it never means.
/// </summary>
public sealed record ConfigInheritedSandbox(
    ConfigResolvedValue<string> ToolchainImage,
    ConfigResolvedValue<int> StepTimeoutSeconds,
    ConfigResolvedValue<int> RunCommandTimeoutSeconds,
    ConfigResolvedValue<string> AgentRegistry,
    ConfigResolvedValue<string> AgentVersion,
    ConfigInheritedResources Resources,
    IReadOnlyDictionary<string, ConfigResolvedValue<string>> Images);

/// <summary>
/// 2026-09-22-6c46: the cpu/memory a project inherits and the NAME of the layer that
/// answers. The layer is on the wire because the value alone cannot say it — the light
/// profile a non-code-changing pipeline is held to and a configured global default can hold
/// the same numbers, and the repository's context document can pre-empt the global default
/// per run.
/// </summary>
public sealed record ConfigInheritedResources(ConfigResourceSummary Values, string Layer);

/// <summary>
/// The per-project rows plus the process-wide row a project with no row of its own falls
/// back to — a project being created, or one being renamed before its save, is not in the
/// running configuration and therefore has none.
/// </summary>
public sealed record InheritedSandboxProjectionResponse(
    ConfigInheritedSandbox ProcessWide,
    IReadOnlyDictionary<string, ConfigInheritedSandbox> Projects);

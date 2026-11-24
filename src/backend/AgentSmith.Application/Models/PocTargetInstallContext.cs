using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dd: context for the PocTargetInstall step — the target the server seeded under
/// ContextKeys.PocTargetName, null when none was.</summary>
public sealed record PocTargetInstallContext(PipelineContext Pipeline, string? Target) : ICommandContext;

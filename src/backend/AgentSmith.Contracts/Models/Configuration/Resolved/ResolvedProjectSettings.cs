using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// p0270a: the materialized effective settings for one project — "what this
/// project will actually run with, and why". Every field carries its provenance.
/// Produced by the single resolution pass and consumed by BOTH the run path and
/// the dashboard, so there is no second resolution that could drift.
///
/// ToolchainImage is RunResolved (Value null) unless a per-project override pins
/// it — the real image is chosen per run from the repo's context.yaml (p0265),
/// never fabricated at config time.
///
/// ResolutionError is non-null when materialization for the dashboard caught a
/// config error (e.g. a missing agent image version) instead of throwing — the
/// run path still fails loud at spawn time, unchanged.
/// </summary>
public sealed record ResolvedProjectSettings(
    string ProjectName,
    ResolvedValue<int> StepTimeoutSeconds,
    ResolvedValue<int> RunCommandTimeoutSeconds,
    ResolvedValue<ResourceLimits> SandboxResources,
    ResolvedValue<string> AgentImage,
    ResolvedValue<string> OrchestratorImage,
    ResolvedValue<string> ToolchainImage,
    ResolvedValue<CostCapValues> CostCap,
    string? ResolutionError = null);

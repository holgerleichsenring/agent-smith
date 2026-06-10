namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0266: process-wide config defaults, redacted for display. PersistenceProvider
/// is the DB KIND only ("sqlite" | "postgresql" | "mysql") — the connection
/// string (which can carry credentials) is never mapped.
/// </summary>
public sealed record ConfigGlobals(
    ConfigSandbox Sandbox,
    ConfigOrchestrator Orchestrator,
    ConfigLimits Limits,
    ConfigCostCap CostCap,
    string PersistenceProvider);

/// <summary>Sandbox defaults: image coordinates and the wall-time caps.</summary>
public sealed record ConfigSandbox(
    string AgentRegistry,
    string AgentVersion,
    int StepTimeoutSeconds,
    int RunCommandTimeoutSeconds);

/// <summary>Orchestrator defaults: image coordinates and the run wall-time ceiling.</summary>
public sealed record ConfigOrchestrator(
    string Registry,
    string Version,
    int MaxRunWallTimeSeconds);

/// <summary>The agentic-loop hard limits operators most often tune.</summary>
public sealed record ConfigLimits(
    int MaxToolCallsPerSkill,
    int MaxLlmCallsPerSkill,
    int MaxConcurrentSkillCalls,
    int MaxSubAgentsPerRun);

/// <summary>The default per-pipeline cost cap (USD + token budget).</summary>
public sealed record ConfigCostCap(decimal Usd, long Tokens);

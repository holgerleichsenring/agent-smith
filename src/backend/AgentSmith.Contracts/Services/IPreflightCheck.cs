using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: one active preflight probe, named after the silent-failure class it
/// prevents (skills pin drift, master-description overflow, rate-limit tier
/// mismatch, unreachable Redis/sandbox, …). Checks exercise the real dependency —
/// every silent failure we shipped had valid-looking config, so linting is not
/// enough. A check that cannot run here (feature unconfigured) returns
/// <see cref="PreflightStatus.Skip"/> with a reason, never a failure. Implementations
/// should not throw — the runner treats an escaped exception as a check bug.
/// </summary>
public interface IPreflightCheck
{
    /// <summary>Stable kebab-case identity, e.g. "skills-catalog".</summary>
    string Name { get; }

    /// <summary>Grouping label for output, e.g. "config", "llm", "infra".</summary>
    string Category { get; }

    Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken);
}

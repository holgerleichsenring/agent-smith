using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0428: one precondition of THIS run, provable in milliseconds without a model call.
/// <para>
/// Distinct from <see cref="IPreflightCheck"/> on purpose: p0324's checks probe global
/// dependencies from a singleton with no run in hand, and pay network round-trips to do
/// it. These read the run's own config, sandboxes and branch, so they take the
/// <see cref="PipelineContext"/>. 2026-11-16-f3fba: a check that runs before the sandbox may read
/// the repository host (its contexts and bootstrap), as ScopeRepos already does; none calls a model.
/// </para>
/// <para>
/// A check with nothing to look at PASSES rather than failing on absence, and an
/// implementation that throws is reported as a warning by the handler — the crash is a
/// bug in the check, not a verdict on the run.
/// </para>
/// </summary>
public interface IRunPreflightCheck
{
    /// <summary>Stable kebab-case identity, e.g. "sandbox-home-writable".</summary>
    string Name { get; }

    /// <summary>
    /// 2026-11-16-f3fba: true when the check reads a sandbox or the checked-out branch and therefore
    /// runs in RunPreflight, after CheckoutSource; false when config and the host decide it, so it runs
    /// in RunConfigPreflight before any sandbox exists.
    /// </summary>
    bool NeedsSandbox { get; }

    Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken);
}

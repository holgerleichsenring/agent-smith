using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0428: one precondition of THIS run, provable in milliseconds without a model call.
/// <para>
/// Distinct from <see cref="IPreflightCheck"/> on purpose: p0324's checks probe global
/// dependencies from a singleton with no run in hand, and pay network round-trips to do
/// it. These read the run's own config, sandboxes and branch, so they take the
/// <see cref="PipelineContext"/> and must never reach the network.
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

    Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken);
}

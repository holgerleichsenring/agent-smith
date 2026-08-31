using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// 2026-08-30-26ed: turns a finished tool run into a <see cref="NucleiResult"/> that says
/// which of the two things happened. A run the runner cut off at its container limit
/// reports the cut-off through the same degraded channel a swagger parse failure uses —
/// a count produced by a stopwatch is not a measurement of the target, and was previously
/// reported as "scan completed" beside the timeout that produced it.
/// </summary>
internal static class NucleiScanOutcome
{
    internal static NucleiResult From(
        ToolResult result,
        IReadOnlyList<NucleiFinding> findings,
        string? degradedReason,
        int limitSeconds)
    {
        var reason = ScanDegradation.Combine(
            degradedReason,
            result.CutOff ? ScanDegradation.CutOffAt(limitSeconds) : null);

        return new NucleiResult(
            findings, result.DurationSeconds, result.Stdout,
            Degraded: reason is not null, DegradedReason: reason);
    }
}

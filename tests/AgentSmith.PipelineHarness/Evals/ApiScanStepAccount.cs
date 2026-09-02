using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: which of the api scan's steps contributed nothing to the score, named.
/// <para>
/// 2026-08-30-26ed made this visible on the run's own result, and for the same reason it
/// belongs beside a score: a step that reported completion at its time limit having found
/// nothing renders identically to a clean target. This tier adds a second silence — the
/// scanner adapters are STUBBED unless an operator opts in — and a score that did not say
/// so would read as a measurement of a scan half of which never executed.
/// </para>
/// </summary>
public static class ApiScanStepAccount
{
    internal const string StubbedNote =
        "stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon "
        + "for dynamic evidence";

    public static IReadOnlyList<string> SilentSteps(PipelineContext pipeline, bool realScanners)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var silent = new List<string>();
        Append(silent, "Nuclei", realScanners,
            Read<NucleiResult>(pipeline, ContextKeys.NucleiResult) is { } nuclei
                ? (nuclei.Findings.Count, nuclei.DegradedReason) : null);
        Append(silent, "Spectral", realScanners,
            Read<SpectralResult>(pipeline, ContextKeys.SpectralResult) is { } spectral
                ? (spectral.Findings.Count, null) : null);
        Append(silent, "ZAP", realScanners,
            Read<ZapResult>(pipeline, ContextKeys.ZapResult) is { } zap
                ? (zap.Findings.Count, zap.DegradedReason) : null);
        AppendSurfaceDifference(pipeline, silent);
        return silent;
    }

    // Four states and only one of them says anything about the target: never ran, ran
    // stubbed, ran and was cut off, ran to its end and found nothing.
    private static void Append(
        List<string> silent, string step, bool realScanners, (int Findings, string? Reason)? result)
    {
        if (result is null) { silent.Add($"{step} (did not run)"); return; }
        if (!realScanners) { silent.Add($"{step} ({StubbedNote})"); return; }
        var (findings, reason) = result.Value;
        if (findings > 0 && reason is null) return;
        silent.Add(reason is not null
            ? $"{step} (contributed {findings} — coverage partial: {reason})"
            : $"{step} (ran to its end and found nothing)");
    }

    // 2026-08-30-c6ec: the difference report always records WHY it was not computed, so an
    // uncomputed difference never reads as an empty one.
    private static void AppendSurfaceDifference(PipelineContext pipeline, List<string> silent)
    {
        if (!pipeline.TryGet<object>(ContextKeys.SurfaceDifference, out var report) || report is null)
            silent.Add("AccountSurfaceDifference (did not run)");
    }

    private static T? Read<T>(PipelineContext pipeline, string key) where T : class =>
        pipeline.TryGet<T>(key, out var value) ? value : null;
}

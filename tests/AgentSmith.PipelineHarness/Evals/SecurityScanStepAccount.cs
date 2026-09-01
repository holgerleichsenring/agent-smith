using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: which of the repository scan's steps contributed nothing to the score,
/// named — the shape 2026-08-30-26ed gave the api scan's dynamic steps.
/// <para>
/// The reason this exists here is concrete and was the first thing to check: the static
/// pattern scanner loads its definitions from the skills catalog, and under the harness's
/// default catalog root it loads ZERO. A scan applying no patterns and a repository with
/// no pattern-visible weakness produce exactly the same empty result, so the count of
/// APPLIED PATTERNS is reported and never inferred.
/// </para>
/// </summary>
public static class SecurityScanStepAccount
{
    /// <summary>Named steps that ran and added nothing to what was scored. An empty list
    /// means every step spoke.</summary>
    public static IReadOnlyList<string> SilentSteps(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var silent = new List<string>();
        AppendStaticPattern(pipeline, silent);
        Append<GitHistoryScanResult>(pipeline, ContextKeys.GitHistoryScanResult,
            r => r.Findings.Count, "GitHistoryScan", silent);
        Append<DependencyAuditResult>(pipeline, ContextKeys.DependencyAuditResult,
            r => r.Findings.Count, "DependencyAudit", silent);
        AppendDegradedTriage(pipeline, silent);
        return silent;
    }

    // Two different silences, and only one of them is about the repository: a scanner with
    // no patterns has not looked, a scanner with patterns and no hits has.
    private static void AppendStaticPattern(PipelineContext pipeline, List<string> silent)
    {
        if (!pipeline.TryGet<StaticScanResult>(ContextKeys.StaticScanResult, out var result)
            || result is null)
        {
            silent.Add("StaticPatternScan (did not run)");
            return;
        }
        if (result.PatternsApplied == 0)
            silent.Add("StaticPatternScan (loaded no pattern definitions — it did not look)");
        else if (result.Findings.Count == 0)
            silent.Add($"StaticPatternScan (applied {result.PatternsApplied} patterns, found nothing)");
    }

    private static void AppendDegradedTriage(PipelineContext pipeline, List<string> silent)
    {
        if (pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason)
            && !string.IsNullOrWhiteSpace(reason))
            silent.Add($"AgenticMaster triage (degraded — {reason})");
    }

    private static void Append<T>(
        PipelineContext pipeline, string key, Func<T, int> findings, string step,
        List<string> silent) where T : class
    {
        if (!pipeline.TryGet<T>(key, out var result) || result is null)
            silent.Add($"{step} (did not run)");
        else if (findings(result) == 0)
            silent.Add($"{step} (found nothing)");
    }
}

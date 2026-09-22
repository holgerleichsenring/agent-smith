using System.Globalization;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-22-7c41a: one sentence for every reader that acts on the per-pipeline cost cap.
/// Six of them do — two stop the run, one skips a skill, two let a phase through unchecked
/// and one ends the master's re-engagement — and until now each said only THAT the cap was
/// exhausted. A resumed run carries the segment before it, so the cap fires earlier and at
/// a reader the operator did not expect; a message that names neither the cap nor the
/// spend turns that into a mystery. This names both, in the units the arm that fired uses.
/// </summary>
public static class CostCapStop
{
    /// <summary>
    /// Tokens are the CACHE-WEIGHTED total the token arm actually compares — cache reads at
    /// one tenth — never the raw sum, which would name a number the cap never read.
    /// Rendered invariantly: the same run must read the same way from any host, and these
    /// figures are matched against the configuration an operator wrote in one notation.
    /// </summary>
    public static string Describe(CostCapValues? cap, decimal spentUsd, long spentTokens)
    {
        var spent = string.Create(CultureInfo.InvariantCulture,
            $"${spentUsd:0.00##} / {spentTokens:N0} cache-weighted tokens");
        if (cap is null) return $"no cost cap is configured; {spent} spent so far";
        var ceiling = string.Create(CultureInfo.InvariantCulture,
            $"${cap.Usd:0.00##} / {cap.Tokens:N0} tokens");
        return $"the run's cost cap ({ceiling}) is exhausted — {spent} spent, "
            + "any segment before a park included";
    }

    /// <summary>The same sentence for a reader holding the run's tracker.</summary>
    public static string Describe(PipelineCostTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return Describe(tracker.CostCap, tracker.EstimateCostUsd(), tracker.EffectiveBudgetTokens);
    }
}

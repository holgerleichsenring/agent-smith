using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Renders the project-context block fed to the planner prompt. Merges the
/// caller-supplied projectContext with whichever upstream artifact the
/// pipeline carries — convergence result (multi-role structured) or
/// consolidated multi-role discussion. Pure formatting; no I/O.
/// </summary>
internal static class PlanContextRenderer
{
    public static string Merge(string? projectContext, PipelineContext pipeline)
    {
        if (pipeline.TryGet<ConvergenceResult>(ContextKeys.ConvergenceResult, out var convergence)
            && convergence is not null)
        {
            var structured = RenderConvergence(convergence);
            return string.IsNullOrEmpty(projectContext) ? structured : $"{projectContext}\n\n{structured}";
        }
        if (pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated)
            && consolidated is not null)
        {
            return string.IsNullOrEmpty(projectContext)
                ? $"## Multi-Role Discussion\n\n{consolidated}"
                : $"{projectContext}\n\n## Multi-Role Discussion\n\n{consolidated}";
        }
        return projectContext ?? string.Empty;
    }

    private static string RenderConvergence(ConvergenceResult convergence)
    {
        var sections = new List<string> { "## Multi-Role Analysis (Structured)" };
        if (convergence.Blocking.Count > 0)
        {
            sections.Add("### Blocking Observations — each MUST map to a plan step");
            foreach (var obs in convergence.Blocking)
            {
                var effort = obs.Effort.HasValue ? $" | effort: {obs.Effort}" : "";
                var loc = obs.DisplayLocation;
                var location = loc != "General" ? $" | target: {loc}" : "";
                sections.Add(
                    $"- [{obs.Id}] **{obs.Concern}** ({obs.Severity}, confidence: {obs.Confidence}){effort}{location}\n"
                    + $"  {obs.Description}\n"
                    + (string.IsNullOrWhiteSpace(obs.Suggestion) ? "" : $"  → Action: {obs.Suggestion}"));
            }
        }
        if (convergence.NonBlocking.Count > 0)
        {
            sections.Add("### Non-Blocking Observations — address if feasible");
            foreach (var obs in convergence.NonBlocking)
                sections.Add(
                    $"- [{obs.Id}] **{obs.Concern}** ({obs.Severity}): {obs.Description}"
                    + (string.IsNullOrWhiteSpace(obs.Suggestion) ? "" : $" → {obs.Suggestion}"));
        }
        if (convergence.Links.Count > 0)
        {
            sections.Add("### Observation Relationships");
            foreach (var link in convergence.Links)
                sections.Add($"- [{link.ObservationId}] {link.Relationship} [{link.RelatedObservationId}]");
        }
        return string.Join("\n\n", sections);
    }
}

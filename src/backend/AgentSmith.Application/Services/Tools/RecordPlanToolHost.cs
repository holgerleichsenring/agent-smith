using System.ComponentModel;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-28-db88b: record_plan — the coding master's plan goes to the run's artifact store, where the run page
/// reads it, instead of a plan.md committed under .agentsmith/runs/. A later call replaces the plan; on a run that
/// works several specs, each spec's master replaces only its own section, so every spec's plan stays on the page.
/// </summary>
public sealed partial class RecordPlanToolHost(IRunArtifactStore store, string runId, string? specId, ILogger logger) : IToolHost
{
    private const string SectionPrefix = "## Plan for spec ";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RecordPlan, name: "record_plan")];
    }

    [Description("Records your plan for this run, in markdown; the run page shows it as the plan. A later call replaces it.")]
    public async Task<string> RecordPlan(
        [Description("The plan in markdown: the files the work will change and the done line each serves.")] string markdown,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "Error: the plan is empty.";
        try
        {
            var plan = specId is null ? markdown : WithSection(await store.ReadPlanMarkdownAsync(runId, ct), markdown);
            await store.WritePlanMarkdownAsync(runId, plan, ct);
            return "Plan recorded; the run page shows it.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "record_plan could not store the plan of {RunId}", runId);
            return "Error: the plan could not be stored; the work can go on without it.";
        }
    }

    // The stored plan as one section per spec, in the order the specs first recorded; this spec's is replaced.
    private string WithSection(string? stored, string markdown)
    {
        var sections = new List<(string Spec, string Body)>();
        foreach (var part in SectionStart().Split(stored ?? string.Empty).Where(p => p.StartsWith(SectionPrefix, StringComparison.Ordinal)))
        {
            var heading = part.IndexOf('\n') is var end and >= 0 ? part[..end] : part;
            sections.Add((heading[SectionPrefix.Length..].Trim(), part));
        }
        var own = $"{SectionPrefix}{specId}\n\n{markdown.Trim()}\n\n";
        var index = sections.FindIndex(s => s.Spec == specId);
        if (index >= 0) sections[index] = (specId!, own);
        else sections.Add((specId!, own));
        return string.Concat(sections.Select(s => s.Body));
    }

    [GeneratedRegex(@"(?m)(?=^## Plan for spec \S+\s*$)")]
    private static partial Regex SectionStart();
}

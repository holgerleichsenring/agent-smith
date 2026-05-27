using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Formats run artifacts (plan.md and result.md) as markdown strings.
/// Pure formatting — no I/O, no state.
/// </summary>
public static class RunResultFormatter
{
    public static string FormatPlan(Ticket ticket, Plan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Plan: {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Ticket:** #{ticket.Id} — {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(plan.Summary);
        sb.AppendLine();
        sb.AppendLine("## Steps");

        foreach (var step in plan.Steps)
            sb.AppendLine($"{step.Order}. [{step.ChangeType}] {step.Description} → {step.TargetFile}");

        return sb.ToString();
    }

    public static string FormatResult(
        Ticket ticket, Plan plan, IReadOnlyList<CodeChange> changes,
        string runId, int durationSeconds, RunCostSummary? costSummary,
        List<ExecutionTrailEntry>? trail, IReadOnlyList<PlanDecision>? decisions = null,
        SecurityTrend? securityTrend = null,
        IReadOnlyList<DialogTrailEntry>? dialogueTrail = null,
        IReadOnlyList<CallCostRecord>? perSkillBreakdown = null,
        RunMetaTopology? topology = null)
    {
        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";

        var sb = new StringBuilder();
        sb.AppendLine($"# Run {RunIdGenerator.FormatForDisplay(runId)}: {ticket.Title}");
        sb.AppendLine();

        RunCostSectionWriter.AppendFrontmatter(sb, ticket, changeType, durationSeconds, costSummary, topology);

        sb.AppendLine("## Changed Files");
        foreach (var change in changes)
            sb.AppendLine($"- [{change.ChangeType}] {change.Path}");

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(plan.Summary);

        RunResultSectionWriter.AppendDecisions(sb, decisions);
        RunResultSectionWriter.AppendDialogueTrail(sb, dialogueTrail);
        RunResultSectionWriter.AppendSecurityTrend(sb, securityTrend);
        RunResultSectionWriter.AppendPerSkillBreakdown(sb, perSkillBreakdown);
        RunResultSectionWriter.AppendExecutionTrail(sb, trail);

        return sb.ToString();
    }

    /// <summary>
    /// p0161d: init-mode plan.md content. The "plan" for init-project is the
    /// list of discovered components plus the bootstrap-skill match per
    /// component — operators can read this before approving any subsequent
    /// runs against the project. Falls back to a "no discovery" note when
    /// the dispatch step preceded this handler without a discovery output
    /// (legacy single-context init path).
    /// </summary>
    public static string FormatInitPlan(
        string runId, string repoName,
        IReadOnlyList<DiscoveredComponent>? components,
        IReadOnlyDictionary<string, string>? bootstrapSkillsByContext = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Init plan {RunIdGenerator.FormatForDisplay(runId)} — repo `{repoName}`");
        sb.AppendLine();
        if (components is null || components.Count == 0)
        {
            sb.AppendLine("_No components discovered for this repo — single-context legacy init path._");
            return sb.ToString();
        }

        sb.AppendLine("## Discovered components");
        sb.AppendLine();
        sb.AppendLine("| Component | Workdir | Language | Evidence | Bootstrap-Skill |");
        sb.AppendLine("|-----------|---------|----------|----------|-----------------|");
        foreach (var c in components)
        {
            var skill = bootstrapSkillsByContext is not null
                && bootstrapSkillsByContext.TryGetValue(c.Name, out var s)
                ? s : "_(deferred)_";
            sb.AppendLine($"| {c.Name} | `{c.Workdir}` | {c.Language} | `{c.Evidence}` | {skill} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Planned writes per component");
        sb.AppendLine();
        foreach (var c in components)
        {
            sb.AppendLine(
                $"- `{c.Name}`: `{ProjectMetaPaths.Contexts}/{c.Name}/{ProjectMetaPaths.ContextYamlFile}`, " +
                $"`{ProjectMetaPaths.Contexts}/{c.Name}/{ProjectMetaPaths.CodingPrinciplesFile}`");
        }
        return sb.ToString();
    }

    /// <summary>
    /// p0130c-followup / p0161d: init-mode result.md brought to parity with
    /// FormatResult. Plan section (Discovery table when components present),
    /// Result section (per-component bootstrap output excerpts), cost +
    /// duration + execution trail + decisions + dialogue + per-skill
    /// breakdown — same shape readers expect from a fix-bug run-doc.
    /// </summary>
    public static string FormatInitResult(
        string runId, int durationSeconds, RunCostSummary? costSummary,
        List<ExecutionTrailEntry>? trail, IReadOnlyList<PlanDecision>? decisions = null,
        IReadOnlyList<DialogTrailEntry>? dialogueTrail = null,
        IReadOnlyList<CallCostRecord>? perSkillBreakdown = null,
        string? repoName = null,
        IReadOnlyList<DiscoveredComponent>? components = null,
        IReadOnlyDictionary<string, string>? bootstrapOutputsByContext = null,
        string? sharedCostNote = null)
    {
        var sb = new StringBuilder();
        var heading = repoName is null
            ? $"# Run {RunIdGenerator.FormatForDisplay(runId)}: init-project"
            : $"# Run {RunIdGenerator.FormatForDisplay(runId)}: init-project — repo `{repoName}`";
        sb.AppendLine(heading);
        sb.AppendLine();
        sb.AppendLine("Bootstrap run — generated per-context `context.yaml` + `coding-principles.md`.");
        sb.AppendLine();

        RunCostSectionWriter.AppendInitFrontmatter(sb, durationSeconds, costSummary);
        AppendDiscoverySection(sb, components);
        AppendBootstrapOutputsSection(sb, components, bootstrapOutputsByContext);
        AppendSharedCostNote(sb, sharedCostNote);

        RunResultSectionWriter.AppendDecisions(sb, decisions);
        RunResultSectionWriter.AppendDialogueTrail(sb, dialogueTrail);
        RunResultSectionWriter.AppendPerSkillBreakdown(sb, perSkillBreakdown);
        RunResultSectionWriter.AppendExecutionTrail(sb, trail);

        return sb.ToString();
    }

    private static void AppendDiscoverySection(
        StringBuilder sb, IReadOnlyList<DiscoveredComponent>? components)
    {
        if (components is null || components.Count == 0) return;
        sb.AppendLine("## Discovered components");
        sb.AppendLine();
        sb.AppendLine("| Component | Workdir | Language | Evidence |");
        sb.AppendLine("|-----------|---------|----------|----------|");
        foreach (var c in components)
            sb.AppendLine($"| {c.Name} | `{c.Workdir}` | {c.Language} | `{c.Evidence}` |");
        sb.AppendLine();
    }

    private static void AppendBootstrapOutputsSection(
        StringBuilder sb, IReadOnlyList<DiscoveredComponent>? components,
        IReadOnlyDictionary<string, string>? outputsByContext)
    {
        if (outputsByContext is null || outputsByContext.Count == 0) return;
        sb.AppendLine("## Bootstrap output per component");
        sb.AppendLine();
        var orderedNames = components?.Select(c => c.Name).ToList() ?? outputsByContext.Keys.ToList();
        foreach (var name in orderedNames)
        {
            if (!outputsByContext.TryGetValue(name, out var output) || string.IsNullOrWhiteSpace(output))
                continue;
            sb.AppendLine($"### {name}");
            sb.AppendLine();
            sb.AppendLine(output.Trim());
            sb.AppendLine();
        }
    }

    private static void AppendSharedCostNote(StringBuilder sb, string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return;
        sb.AppendLine($"_{note}_");
        sb.AppendLine();
    }
}

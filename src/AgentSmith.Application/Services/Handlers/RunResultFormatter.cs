using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
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
        int runNumber, int durationSeconds, RunCostSummary? costSummary,
        List<ExecutionTrailEntry>? trail, IReadOnlyList<PlanDecision>? decisions = null,
        SecurityTrend? securityTrend = null,
        IReadOnlyList<DialogTrailEntry>? dialogueTrail = null)
    {
        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";

        var sb = new StringBuilder();
        sb.AppendLine($"# r{runNumber:D2}: {ticket.Title}");
        sb.AppendLine();

        RunCostSectionWriter.AppendFrontmatter(sb, ticket, changeType, durationSeconds, costSummary);

        sb.AppendLine("## Changed Files");
        foreach (var change in changes)
            sb.AppendLine($"- [{change.ChangeType}] {change.Path}");

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(plan.Summary);

        RunResultSectionWriter.AppendDecisions(sb, decisions);
        RunResultSectionWriter.AppendDialogueTrail(sb, dialogueTrail);
        RunResultSectionWriter.AppendSecurityTrend(sb, securityTrend);
        RunResultSectionWriter.AppendExecutionTrail(sb, trail);

        return sb.ToString();
    }
}

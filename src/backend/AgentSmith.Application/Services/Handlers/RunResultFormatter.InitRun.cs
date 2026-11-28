using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-28-db88b: what one repository of an init discovered, bootstrapped and retired.</summary>
public sealed record InitRepoRecord(
    string RepoName,
    IReadOnlyList<DiscoveredComponent>? Components,
    IReadOnlyDictionary<string, string>? BootstrapOutputs,
    IReadOnlyList<string>? RetiredContexts);

public static partial class RunResultFormatter
{
    /// <summary>
    /// 2026-11-28-db88b: an init's ONE result, joining every repository's components, bootstrap output and retired
    /// contexts under the run's frontmatter, decisions and trail — the run page shows one result per run.
    /// </summary>
    public static string FormatInitResult(
        string runId, int durationSeconds, RunCostSummary? costSummary,
        List<ExecutionTrailEntry>? trail, IReadOnlyList<PlanDecision>? decisions,
        IReadOnlyList<DialogTrailEntry>? dialogueTrail, IReadOnlyList<CallCostRecord>? perSkillBreakdown,
        IReadOnlyList<InitRepoRecord> repositories, string? sharedCostNote)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var sb = new StringBuilder();
        sb.AppendLine($"# Run {RunIdGenerator.FormatForDisplay(runId)}: init-project");
        sb.AppendLine();
        sb.AppendLine("Bootstrap run — generated per-context `context.yaml` + `principles.md`.");
        sb.AppendLine();

        RunCostSectionWriter.AppendInitFrontmatter(sb, durationSeconds, costSummary);
        foreach (var repository in repositories)
        {
            if (repositories.Count > 1)
            {
                sb.AppendLine($"## Repository `{repository.RepoName}`");
                sb.AppendLine();
            }
            AppendDiscoverySection(sb, repository.Components);
            AppendRetiredSection(sb, repository.RetiredContexts);
            AppendBootstrapOutputsSection(sb, repository.Components, repository.BootstrapOutputs);
        }
        AppendSharedCostNote(sb, sharedCostNote);

        RunResultSectionWriter.AppendDecisions(sb, decisions);
        RunResultSectionWriter.AppendDialogueTrail(sb, dialogueTrail);
        RunResultSectionWriter.AppendPerSkillBreakdown(sb, perSkillBreakdown);
        RunResultSectionWriter.AppendExecutionTrail(sb, trail);
        return sb.ToString();
    }
}

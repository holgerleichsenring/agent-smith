using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Renders the sections the master's user prompt is assembled from — working
/// state, plan, code map, memory index, repo names, project context. Pure text
/// over pipeline state; p0403 lifted them out of the handler.
/// </summary>
internal static class MasterPromptSections
{
    // p0341c: the continuity carry rendered into a re-engagement pass — decisions committed
    // so far + the last build/test tail. Pure so it is unit-testable in isolation.
    internal static string BuildWorkingStateBlock(
        IReadOnlyList<PlanDecision> decisions, MasterVerification? verification)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Working state (carry this forward)");
        if (decisions is { Count: > 0 })
        {
            sb.AppendLine("Decisions committed so far:");
            foreach (var d in decisions.Take(12))
                sb.AppendLine($"- [{d.Category}] {d.Decision}");
        }
        else
        {
            sb.AppendLine("Decisions committed so far: (none logged yet)");
        }
        var tail = verification?.Summary;
        sb.AppendLine("Last build/test: "
            + (string.IsNullOrWhiteSpace(tail)
                ? $"status {verification?.Status.ToString() ?? "not yet run"}"
                : tail));
        sb.AppendLine();
        return sb.ToString();
    }

    // p0341c: project the RemoteContextInventory (repo name → discovered contexts) into a
    // repo-name → context-name-list map for the write_context_yaml guard. Absent inventory
    // (bootstrap runs, --context override) => null, so the guard is a no-op.
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildDiscoveredContexts(
        PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.RemoteContextInventory, out var inv) || inv is null || inv.Count == 0)
            return null;
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repoName, discoveries) in inv)
            map[repoName] = discoveries
                .Select(d => d.ContextName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        return map;
    }

    internal static string BuildProjectContextSection(string? projectContext) =>
        string.IsNullOrWhiteSpace(projectContext)
            ? string.Empty
            : $"## Project Context\n{projectContext}\n";

    // p0384: one sub-section per repo so the master's structural knowledge covers
    // EVERY scoped repo, not the arbitrary first one.
    internal static string BuildCodeMapSection(IReadOnlyDictionary<string, string>? repoCodeMaps)
    {
        var maps = repoCodeMaps?
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToList() ?? [];
        if (maps.Count == 0) return string.Empty;
        var sections = string.Join("\n", maps.Select(kv => $"### Repository: {kv.Key}\n{kv.Value}\n"));
        return $"## Code Map\n{sections}";
    }

    // p0380: the plan-time memory INDEX (LoadMemoryIndex). One line per memory;
    // bodies stay on disk and are pulled via recall(). Absent store => empty.
    internal static string BuildMemoryIndexSection(PipelineContext pipeline)
    {
        var index = pipeline.TryGet<string>(ContextKeys.MemoryIndex, out var mi) ? mi : null;
        if (string.IsNullOrWhiteSpace(index)) return string.Empty;
        return "## Experiential memory index\n"
               + "One line per stored memory. Use the recall tool to load a memory's body "
               + "before re-deriving a known fact; use remember to propose a new one.\n\n"
               + index.Trim() + "\n";
    }

    // p0394a: render the ratified phase spec into the master body as the plan of
    // record — goal, steps and done criteria verbatim, so the master executes the
    // same artifact the ledger seeded from and the keystone verifies. Empty when
    // the run carries no phase spec (scan / spec-dialog surfaces) — the skill
    // omits the section then.
    internal static string BuildPlanSection(PhaseDraft? draft)
    {
        if (draft is null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.Append("## The phase spec — the plan of record\n\n");
        sb.Append($"**Goal:** {draft.Goal}\n");
        if (draft.Steps.Count > 0)
        {
            sb.Append("\nSteps:\n");
            foreach (var step in draft.Steps)
                sb.Append($"  [{step.Id}] {step.Action}"
                    + (step.Target is null ? "\n" : $" (target: {step.Target})\n"));
        }
        if (draft.Done.Count > 0)
        {
            sb.Append("\nDone criteria:\n");
            foreach (var criterion in draft.Done)
                sb.Append($"- {criterion}\n");
        }
        return sb.ToString();
    }

    // p0179h: list of repo (sandbox) names the master can address in this run.
    // Empty for 0-1 sandboxes (no prefix needed in the prompt body), bullet
    // list under a "Repositories in this run" heading for 2+. Coupled with
    // FilesystemToolHost.Route accepting the prefix in single-sandbox mode so
    // the master can use one consistent path convention.
    internal static string BuildRepoNamesSection(IEnumerable<string> sandboxKeys)
    {
        var names = sandboxKeys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (names.Count <= 1) return string.Empty;
        var bullets = string.Join("\n", names.Select(n => $"- `{n}`"));
        return $"## Repositories in this run\n{bullets}\n";
    }
}

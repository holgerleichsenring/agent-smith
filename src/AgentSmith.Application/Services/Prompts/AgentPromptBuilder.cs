using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Shared prompt building logic for plan generation and execution.
/// Migrated from Infrastructure during the M.E.AI refactor (p0119a) so
/// Application-layer handlers can reach it without an Infrastructure dependency.
/// </summary>
public sealed class AgentPromptBuilder(IPromptCatalog prompts)
{
    public string BuildPlanSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        return prompts.Render("agent-plan-system", new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(projectContext),
            ["CodingPrinciples"] = codingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(codeMap),
        });
    }

    public string BuildPlanUserPrompt(
        Ticket ticket, ProjectMap projectMap,
        IReadOnlyDictionary<string, string>? planAnswers = null)
    {
        var modules = string.Join('\n', projectMap.Modules
            .Where(m => m.Role == ModuleRole.Production)
            .Select(m => $"  - {m.Path}"));
        var testProjects = projectMap.TestProjects.Count == 0 ? "(none)" :
            string.Join('\n', projectMap.TestProjects.Select(t =>
                $"  - {t.Path} ({t.Framework}, {t.FileCount} test file(s))"));
        var entryPoints = projectMap.EntryPoints.Count == 0 ? "(none discovered)" :
            string.Join('\n', projectMap.EntryPoints.Select(e => $"  - {e}"));
        var frameworks = projectMap.Frameworks.Count == 0 ? "Unknown" :
            string.Join(", ", projectMap.Frameworks);

        return $"""
            ## Ticket
            **ID:** {ticket.Id}
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}

            ## Codebase Analysis
            **Language:** {projectMap.PrimaryLanguage}
            **Frameworks:** {frameworks}

            ### Modules (production)
            {modules}

            ### Test Projects
            {testProjects}

            ### Entry Points
            {entryPoints}
            {BuildOperatorAnswersSection(planAnswers)}
            """;
    }

    internal static string BuildOperatorAnswersSection(
        IReadOnlyDictionary<string, string>? planAnswers)
    {
        if (planAnswers is null || planAnswers.Count == 0)
            return "";

        var lines = string.Join('\n', planAnswers
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"  - **Q{kv.Key}:** {kv.Value}"));

        return $"""


            ## Operator answers to prior open questions
            The previous Plan asked clarifying questions and was halted. The operator's answers below
            are authoritative — incorporate them into the new Plan and produce status=complete.
            {lines}
            """;
    }

    public string BuildExecutionSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        return prompts.Render("agent-execute-system", new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(projectContext),
            ["CodingPrinciples"] = codingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(codeMap),
        });
    }

    public string BuildExecutionUserPrompt(
        Plan plan, Repository repository, string? verifyNotes = null,
        IReadOnlyList<string>? repoNames = null,
        IReadOnlyDictionary<string, string>? repoLanguages = null)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}
            {BuildReposInScopeSection(repoNames, repoLanguages)}{BuildVerifyNotesSection(verifyNotes)}
            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            Start by listing the relevant files, then implement each step.
            """;
    }

    /// <summary>
    /// p0158e: when the run spans multiple repos, list them so the agent knows
    /// to use repo-qualified paths in filesystem tool calls and pass `repo` to
    /// run_command. p0158f: include per-repo PrimaryLanguage when available so
    /// the agent picks the right test command per repo. Single-repo runs emit
    /// nothing (back-compat).
    /// </summary>
    internal static string BuildReposInScopeSection(
        IReadOnlyList<string>? repoNames,
        IReadOnlyDictionary<string, string>? repoLanguages = null)
    {
        if (repoNames is null || repoNames.Count <= 1) return string.Empty;
        var bullets = string.Join("\n", repoNames.Select(name =>
        {
            var lang = repoLanguages is not null && repoLanguages.TryGetValue(name, out var l) ? l : null;
            return lang is null ? $"  - {name}" : $"  - {name} ({lang})";
        }));
        return $"""


            ## Repos in scope
            This run spans {repoNames.Count} repos:
            {bullets}
            Use repo-qualified paths in filesystem tool calls (e.g. `{repoNames[0]}/src/Foo.cs`).
            Pass `repo` to run_command (e.g. run_command(command="dotnet test", repo="{repoNames[0]}")).
            """;
    }

    internal static string BuildVerifyNotesSection(string? verifyNotes)
    {
        if (string.IsNullOrWhiteSpace(verifyNotes)) return "";
        return $"""


            ## Prior verify-phase observations
            The previous implementation produced a Diff that the verify phase flagged.
            Apply these to the next implementation pass.
            {verifyNotes}

            """;
    }

    internal static string BuildProjectContextSection(string? projectContext)
    {
        if (string.IsNullOrWhiteSpace(projectContext))
            return "";

        return $"""

            ## Project Context
            This describes the project's identity, architecture, and recent history.
            ```yaml
            {projectContext}
            ```

            """;
    }

    internal static string BuildCodeMapSection(string? codeMap)
    {
        if (string.IsNullOrWhiteSpace(codeMap))
            return "";

        return $"""

            ## Code Map
            Use this map to understand module boundaries, interface contracts, and dependency flow.
            ```yaml
            {codeMap}
            ```

            """;
    }
}

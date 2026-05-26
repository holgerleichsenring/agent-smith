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
        IReadOnlyList<string>? contextKeys = null,
        IReadOnlyDictionary<string, string>? perKeyLanguages = null,
        string? appliesTo = null)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}
            {BuildAppliesToSection(appliesTo)}{BuildContextsInScopeSection(contextKeys, perKeyLanguages)}{BuildVerifyNotesSection(verifyNotes)}
            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            Start by listing the relevant files, then implement each step.
            """;
    }

    /// <summary>
    /// p0161d: phase-spec applies_to renders as a single "Applies to: ..." line
    /// near the top of the user prompt so the LLM knows which stack(s) the
    /// active phase targets. Empty/missing value emits nothing — back-compat
    /// for pipelines that don't carry a phase spec.
    /// </summary>
    internal static string BuildAppliesToSection(string? appliesTo)
    {
        if (string.IsNullOrWhiteSpace(appliesTo)) return string.Empty;
        return $"Applies to: {appliesTo}\n";
    }

    /// <summary>
    /// p0158e + p0161a: when the run spans multiple discovered contexts
    /// (composite sandbox keys after p0161a — "default" / "&lt;ctx&gt;" /
    /// "&lt;repo&gt;" / "&lt;repo&gt;/&lt;ctx&gt;"), list them so the agent
    /// uses context-qualified paths in filesystem tool calls and passes the
    /// key to run_command. Per-key annotation is the context's
    /// PrimaryLanguage from its ProjectMap. Single-context runs emit nothing
    /// (back-compat).
    /// </summary>
    internal static string BuildContextsInScopeSection(
        IReadOnlyList<string>? contextKeys,
        IReadOnlyDictionary<string, string>? perKeyLanguages = null)
    {
        if (contextKeys is null || contextKeys.Count <= 1) return string.Empty;
        var bullets = string.Join("\n", contextKeys.Select(key =>
        {
            var lang = perKeyLanguages is not null && perKeyLanguages.TryGetValue(key, out var l) ? l : null;
            return lang is null ? $"  - {key}" : $"  - {key} ({lang})";
        }));
        return $"""


            ## Contexts in scope
            This run spans {contextKeys.Count} contexts:
            {bullets}
            Use context-qualified paths in filesystem tool calls (e.g. `{contextKeys[0]}/src/Foo.cs`).
            Pass the context key to run_command (e.g. run_command(command="dotnet test", repo="{contextKeys[0]}")).
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

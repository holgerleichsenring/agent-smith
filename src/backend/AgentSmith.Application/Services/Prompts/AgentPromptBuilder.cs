using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Composes the execution system/user prompts for the coding master.
/// Migrated from Infrastructure during the M.E.AI refactor (p0119a) so
/// Application-layer handlers can reach it without an Infrastructure dependency.
/// p0415: the plan-prompt half went with the retired GeneratePlan command.
/// </summary>
public sealed class AgentPromptBuilder
{
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
            An earlier pass of this run asked the operator the questions below and paused. The answers
            are authoritative — act on them, continue the work, and do not ask them again.
            {lines}
            """;
    }

    /// <summary>
    /// 2026-11-09-0620c: the system prompt of a pass that follows a change — tests or documentation. It used to be the
    /// whole coding master with its run tokens bound empty ("up to  attempts", a verdict block nobody parses), for a
    /// call that has none of that master's surface. 2026-11-09-0620k: the project's principles, context and code map
    /// come first, in the coding master's own order, and the pass's job and reach follow.
    /// </summary>
    public string BuildChangePassSystemPrompt(
        ChangePass pass, string codingPrinciples, IReadOnlyDictionary<string, string>? repoCodeMaps,
        string? projectContext = null) =>
        $"""
        ## Coding Principles
        {codingPrinciples}
        {BuildProjectContextSection(projectContext)}
        {BuildCodeMapSection(repoCodeMaps)}
        {ChangePassText.For(pass)}
        """;

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
            """;
    }

    /// <summary>
    /// p0161d: spec applies_to renders as a single "Applies to: ..." line
    /// near the top of the user prompt so the LLM knows which stack(s) the
    /// active spec targets. Empty/missing value emits nothing — back-compat
    /// for pipelines that don't carry a spec.
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

    // p0384: one fenced map per repo — the heading names the repo so module
    // boundaries are never attributed to the wrong codebase.
    internal static string BuildCodeMapSection(IReadOnlyDictionary<string, string>? repoCodeMaps)
    {
        var maps = repoCodeMaps?
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToList() ?? [];
        if (maps.Count == 0) return "";

        var sections = string.Join("\n", maps.Select(kv => $"""
            ### Repository: {kv.Key}
            ```yaml
            {kv.Value}
            ```
            """));
        return $"""

            ## Code Map
            Use this map to understand module boundaries, interface contracts, and dependency flow.
            {sections}

            """;
    }
}

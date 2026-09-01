using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Builds the (system, user) prompt pair for a bootstrap-round skill. The
/// system prompt is the role description + rules; the user prompt embeds the
/// serialized ProjectMap, names the target context's MetaDir, and explains the
/// required tool flow.
///
/// p0202d: on re-init (the existing context.yaml / principles.md are
/// passed in non-null), the prompt switches from generate-from-scratch to
/// preserve-and-merge — operator content is kept, only missing/stale fields are
/// filled (notably prerequisites). Cold-init (both null) is unchanged.
///
/// p0379: when the framework transferred the composed core+delta principles
/// (or preserved a ratified file), the prompt names context.yaml as the only
/// write target and asks the skill's summary to request operator ratification
/// — the skill never authors or merges principles.md in those modes.
/// </summary>
internal static class BootstrapPromptFactory
{
    public static (string System, string User) Build(
        RoleSkillDefinition role, Repository repository, ProjectMap projectMap,
        string contextName, string workdir, string? appliesTo = null,
        string? existingContextYaml = null, string? existingPrinciples = null,
        PrinciplesMode principlesMode = PrinciplesMode.SkillWrites)
    {
        var system = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;
        var projectMapJson = JsonSerializer.Serialize(
            projectMap, new JsonSerializerOptions { WriteIndented = true });
        var (contextYamlPath, principlesPath) = ResolveTargetPaths(contextName);
        var appliesToLine = string.IsNullOrWhiteSpace(appliesTo)
            ? string.Empty
            : $"\nApplies to: {appliesTo}\n";
        // p0379: in transfer/preserve mode the principles file is framework-
        // owned — only an existing context.yaml flips the prompt to merge, and
        // the existing principles are never embedded for rewriting.
        var skillWritesPrinciples = principlesMode == PrinciplesMode.SkillWrites;
        var mergeablePrinciples = skillWritesPrinciples ? existingPrinciples : null;
        var isReInit = !string.IsNullOrWhiteSpace(existingContextYaml)
                    || !string.IsNullOrWhiteSpace(mergeablePrinciples);
        var user = $"""
            ## Component
            - Context name: {contextName}
            - Workdir (repo-relative): {workdir}{appliesToLine}

            ## ProjectMap (from AnalyzeCode)

            ```json
            {projectMapJson}
            ```

            ## Repository
            - Branch: {repository.CurrentBranch.Value}
            - Local path: {repository.LocalPath}
            {ReInitSection(isReInit, existingContextYaml, mergeablePrinciples)}
            {WriteInstruction(isReInit, contextYamlPath, principlesPath, principlesMode)}
            """;
        return (system, user);
    }

    private static string ReInitSection(
        bool isReInit, string? existingContextYaml, string? existingPrinciples)
    {
        if (!isReInit) return string.Empty;
        var principlesBlock = string.IsNullOrWhiteSpace(existingPrinciples)
            ? string.Empty
            : $"""


              ### Existing principles.md
              ```
              {existingPrinciples}
              ```
              """;
        return $"""

            ## Existing files (RE-INIT — preserve and merge)
            This component is ALREADY initialized. Do NOT regenerate from
            scratch. Start from the existing files below: keep every field the
            operator authored verbatim, and only (a) fill fields that are
            clearly missing or empty; (b) correct fields that are clearly stale
            versus the current source. Never drop an operator field. (The
            environment-prepare command is analyzer-derived as `prerequisites` —
            do not write it here unless the operator already set an override.)

            ### Existing context.yaml
            ```
            {existingContextYaml}
            ```{principlesBlock}
            """;
    }

    private static string WriteInstruction(
        bool isReInit, string contextYamlPath, string principlesPath, PrinciplesMode principlesMode)
    {
        var lead = isReInit
            ? "Read source files via your read-only tools to confirm the merge, then write the MERGED files:"
            : "Read source files via your read-only tools to ground claims for THIS component, then write:";
        // p0193-fix: context.yaml goes through write_context_yaml (write_file
        // rejects context.yaml paths); principles.md through write_file.
        if (principlesMode == PrinciplesMode.SkillWrites)
        {
            return $"""
                {lead}
                  - `{contextYamlPath}` — use the `write_context_yaml` tool (NOT write_file;
                    the framework rejects write_file for context.yaml).
                  - `{principlesPath}` — use the `write_file` tool.
                After both writes succeed, return a short Markdown summary of the
                choices you made (per `output_schema: bootstrap`).
                """;
        }

        // p0379: the principles file is framework-owned in transfer/preserve
        // mode — the skill writes facts (context.yaml) and requests ratification.
        var principlesLine = principlesMode == PrinciplesMode.Transferred
            ? $"`{principlesPath}` is already in place — transferred from the "
              + "authored universal core plus this component's language delta."
            : $"`{principlesPath}` already exists and is preserved as ratified.";
        return $"""
            {lead}
              - `{contextYamlPath}` — use the `write_context_yaml` tool (NOT write_file;
                the framework rejects write_file for context.yaml).

            Principles: {principlesLine} Leave that file as is.
            After the context.yaml write succeeds, return a short Markdown summary
            of the choices you made (per `output_schema: bootstrap`) and ask the
            operator to RATIFY the principles by reviewing
            `{principlesPath}` in the init pull request — this project's own rules,
            about its ENVIRONMENT as much as its code, are appended under its
            "Project Specifics" section and survive re-runs.
            """;
    }

    internal static (string ContextYaml, string Principles) ResolveTargetPaths(string contextName)
    {
        if (string.IsNullOrEmpty(contextName))
            return (ProjectMetaPaths.ContextYaml, ProjectMetaPaths.Principles);
        var metaDir = $"{ProjectMetaPaths.Contexts}/{contextName}";
        return ($"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
                $"{metaDir}/{ProjectMetaPaths.PrinciplesFile}");
    }
}

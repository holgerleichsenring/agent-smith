using System.Text.Json;
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
/// p0202d: on re-init (the existing context.yaml / coding-principles.md are
/// passed in non-null), the prompt switches from generate-from-scratch to
/// preserve-and-merge — operator content is kept, only missing/stale fields are
/// filled (notably prerequisites). Cold-init (both null) is unchanged.
/// </summary>
internal static class BootstrapPromptFactory
{
    public static (string System, string User) Build(
        RoleSkillDefinition role, Repository repository, ProjectMap projectMap,
        string contextName, string workdir, string? appliesTo = null,
        string? existingContextYaml = null, string? existingCodingPrinciples = null)
    {
        var system = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;
        var projectMapJson = JsonSerializer.Serialize(
            projectMap, new JsonSerializerOptions { WriteIndented = true });
        var (contextYamlPath, codingPrinciplesPath) = ResolveTargetPaths(contextName);
        var appliesToLine = string.IsNullOrWhiteSpace(appliesTo)
            ? string.Empty
            : $"\nApplies to: {appliesTo}\n";
        var isReInit = !string.IsNullOrWhiteSpace(existingContextYaml)
                    || !string.IsNullOrWhiteSpace(existingCodingPrinciples);
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
            {ReInitSection(isReInit, existingContextYaml, existingCodingPrinciples)}
            {WriteInstruction(isReInit, contextYamlPath, codingPrinciplesPath)}
            """;
        return (system, user);
    }

    private static string ReInitSection(
        bool isReInit, string? existingContextYaml, string? existingCodingPrinciples)
    {
        if (!isReInit) return string.Empty;
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
            ```

            ### Existing coding-principles.md
            ```
            {existingCodingPrinciples}
            ```
            """;
    }

    private static string WriteInstruction(bool isReInit, string contextYamlPath, string codingPrinciplesPath)
    {
        var verb = isReInit
            ? "Read source files via your read-only tools to confirm the merge, then use the WriteFile tool to emit the MERGED"
            : "Read source files via your read-only tools to ground claims for THIS component, then use the WriteFile tool to emit";
        return $"""
            {verb}:
              - `{contextYamlPath}`
              - `{codingPrinciplesPath}`
            After both writes succeed, return a short Markdown summary of the
            choices you made (per `output_schema: bootstrap`).
            """;
    }

    internal static (string ContextYaml, string CodingPrinciples) ResolveTargetPaths(string contextName)
    {
        if (string.IsNullOrEmpty(contextName))
            return (ProjectMetaPaths.ContextYaml, ProjectMetaPaths.CodingPrinciples);
        var metaDir = $"{ProjectMetaPaths.Contexts}/{contextName}";
        return ($"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
                $"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}");
    }
}

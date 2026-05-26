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
/// required tool flow (read source -> WriteFile context.yaml + coding-principles.md
/// at the per-context paths -> return markdown summary per output_schema: bootstrap).
///
/// p0161d: the user prompt's WriteFile targets are constructed from
/// <see cref="ProjectMetaPaths.MetaDirFor"/> for the round's ContextName.
/// Path strings are no longer hardcoded in the skill body — the user prompt is
/// canonical for WHERE to write. Workdir tells the LLM which sub-tree of the
/// repo this component lives in (relevant when reading for grounding evidence).
/// </summary>
internal static class BootstrapPromptFactory
{
    public static (string System, string User) Build(
        RoleSkillDefinition role, Repository repository, ProjectMap projectMap,
        string contextName, string workdir, string? appliesTo = null)
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

            Read source files via your read-only tools to ground claims for THIS
            component (under `{workdir}` if it's a sub-tree, or the repo root if
            workdir=`.`). Then use the WriteFile tool to emit:
              - `{contextYamlPath}`
              - `{codingPrinciplesPath}`
            After both writes succeed, return a short Markdown summary of the
            choices you made (per `output_schema: bootstrap`).
            """;
        return (system, user);
    }

    private static (string ContextYaml, string CodingPrinciples) ResolveTargetPaths(string contextName)
    {
        if (string.IsNullOrEmpty(contextName))
            return (ProjectMetaPaths.ContextYaml, ProjectMetaPaths.CodingPrinciples);
        var metaDir = $"{ProjectMetaPaths.Contexts}/{contextName}";
        return ($"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
                $"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}");
    }
}

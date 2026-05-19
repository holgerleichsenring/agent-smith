using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Builds the (system, user) prompt pair for a bootstrap-round skill. The
/// system prompt is the role description + rules; the user prompt embeds the
/// serialized ProjectMap and explains the required tool flow (read source ->
/// WriteFile .agentsmith/context.yaml + coding-principles.md -> return markdown
/// summary per output_schema: bootstrap).
/// </summary>
internal static class BootstrapPromptFactory
{
    public static (string System, string User) Build(
        RoleSkillDefinition role, Repository repository, ProjectMap projectMap)
    {
        var system = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;
        var projectMapJson = JsonSerializer.Serialize(
            projectMap, new JsonSerializerOptions { WriteIndented = true });
        var user = $"""
            ## ProjectMap (from AnalyzeCode)

            ```json
            {projectMapJson}
            ```

            ## Repository
            - Branch: {repository.CurrentBranch.Value}
            - Local path: {repository.LocalPath}

            Read source files via your read-only tools as needed to ground claims
            (csproj/package.json, top-level Program.cs / index.ts, sample test).
            Then use the WriteFile tool to emit:
              - `.agentsmith/context.yaml`
              - `.agentsmith/coding-principles.md`
            After both writes succeed, return a short Markdown summary of the
            choices you made (per `output_schema: bootstrap`).
            """;
        return (system, user);
    }
}

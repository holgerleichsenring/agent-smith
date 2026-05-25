using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0161d: builds the (system, user) prompt pair for the read-only
/// BootstrapDiscover round. The system prompt is the project-discovery
/// skill's role description + rules. The user prompt embeds the repo-level
/// ProjectMap (from AnalyzeCode), names the repo root and the
/// component-criteria anchor, and specifies the discovery output_schema.
///
/// The LLM's tool surface is read-only filesystem + ask_human; it returns
/// a structured discovery JSON document that the handler parses into
/// <see cref="DiscoveredComponent"/> entries.
/// </summary>
internal static class BootstrapDiscoverPromptFactory
{
    public static (string System, string User) Build(
        RoleSkillDefinition role, Repository repository, string repoName,
        ProjectMap projectMap, bool isInteractive)
    {
        var system = $$"""
            ## Your Role
            {{role.DisplayName}}: {{role.Description}}

            ## Role-Specific Rules
            {{role.Rules}}
            """;
        var projectMapJson = JsonSerializer.Serialize(
            projectMap, new JsonSerializerOptions { WriteIndented = true });
        var ambiguityGuidance = isInteractive
            ? "If you cannot disambiguate two candidates from the tree alone, call `ask_human` once with the conflicting evidence."
            : "If you cannot disambiguate two candidates from the tree alone, return status=\"ambiguous\" with the candidates listed under `ambiguity` — DO NOT guess.";
        var user = $$"""
            ## Repo
            - Name: {{repoName}}
            - Branch: {{repository.CurrentBranch.Value}}
            - Local path: {{repository.LocalPath}}

            ## ProjectMap (from AnalyzeCode)

            ```json
            {{projectMapJson}}
            ```

            ## Your task

            Enumerate the **independently-deployable or independently-callable
            components** in this repo. A component is proved by an entrypoint
            (e.g. `Program.cs`, `main.go`, `index.ts` in a package root) OR a
            deploy artefact (e.g. `Dockerfile`, `k8s/`, `Procfile`, `vercel.json`).
            A consumed library without either is NOT a component.

            Use your read-only tools (`directory_tree`, `read_file`,
            `list_directory`, `find_files`, `grep_in_tree`) freely — depth over
            speed. There is no read-call cap. Confirm each component's language
            from the same Read pass that proved it.

            {{ambiguityGuidance}}

            ## Output

            Return ONE JSON document, no prose, no markdown fence — matching
            `output_schema: discovery`:

            ```
            {
              "status": "complete",
              "components": [
                {
                  "name": "<lowercase slug, no slashes — used as the .agentsmith/contexts/<name>/ directory>",
                  "workdir": "<repo-relative path, e.g. \".\" for single-component repos or \"server\" for a sub-tree>",
                  "language": "<free-form language slug — csharp/typescript/python/go/markdown/...>",
                  "evidence": "<path of the entrypoint or deploy artefact that proves this component>"
                }
              ]
            }
            ```

            For single-component repos, return exactly one entry with
            `workdir="."`. For ambiguity, return `status="ambiguous"` with
            `ambiguity.message` + `ambiguity.candidates`.
            """;
        return (system, user);
    }
}

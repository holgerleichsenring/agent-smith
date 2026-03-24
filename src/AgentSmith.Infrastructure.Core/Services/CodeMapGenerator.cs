using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Generates a code-map.yaml for a repository using one cheap LLM call.
/// Maps modules, interfaces, implementations, key classes, and dependencies.
/// All language-specific interpretation is done by the LLM, not by code.
/// </summary>
public sealed class CodeMapGenerator(
    ILogger<CodeMapGenerator> logger) : ICodeMapGenerator
{
    public async Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Generating code-map.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var userPrompt = BuildUserPrompt(project, snapshot);
        var llmResponse = await llmClient.CompleteAsync(
            SystemPrompt, userPrompt, TaskType.CodeMapGeneration, cancellationToken);

        var yaml = LlmResponseHelper.StripCodeFences(llmResponse.Text);

        if (!LlmResponseHelper.IsValidYaml(yaml))
        {
            logger.LogWarning("Generated code map is not valid YAML, returning empty");
            return string.Empty;
        }

        logger.LogInformation("Generated code-map.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    internal static string BuildUserPrompt(DetectedProject project, RepoSnapshot snapshot)
    {
        var codeSection = snapshot.CodeSamples.Count > 0
            ? "## Code Samples\n" + string.Join('\n', snapshot.CodeSamples)
            : "";

        var configSection = snapshot.ConfigFileContents.Count > 0
            ? "## Config Files\n" + string.Join('\n', snapshot.ConfigFileContents)
            : "";

        return $"""
            ## Project
            Language: {project.Language}
            Runtime: {project.Runtime ?? "unknown"}
            Frameworks: [{string.Join(", ", project.Frameworks)}]

            ## Directory Structure
            {snapshot.DirectoryTree}

            {codeSection}

            {configSection}

            ## Output Format
            Generate a code-map.yaml with this structure:

            ```yaml
            modules:
              - name: <module/project name>
                path: <relative path>
                depends_on: [<other module names>]
                interfaces:
                  - name: <interface name>
                    file: <relative path>
                    does: "<one-line description>"
                    implementations:
                      - name: <class name>
                        file: <relative path>
                key_classes:
                  - name: <class name>
                    file: <relative path>
                    does: "<one-line description>"

            entry_points:
              - file: <relative path>
                type: <main|di_setup|route_config|middleware>
                does: "<one-line description>"

            dependency_graph:
              <module_name>: [<depends_on_module>, ...]
            ```

            Generate the code-map.yaml. Return ONLY valid YAML.
            """;
    }

    private const string SystemPrompt = """
        You are a code architecture analyst. Generate a code-map in YAML format for this repository.

        Rules:
        - List all modules/projects and their dependencies on each other
        - For each module: list key interfaces and their implementations
        - Identify entry points (main, DI setup, route config)
        - For architecturally significant classes: one-line "does" description
        - Return ONLY valid YAML, no explanation or markdown fences
        - Be precise and concise — this is a reference map, not documentation
        """;
}

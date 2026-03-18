using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// Input: DetectedProject + key file contents + RepoSnapshot (with directory tree).
/// Output: raw YAML string.
/// </summary>
public sealed class ContextGenerator(
    ILogger<ContextGenerator> logger) : IContextGenerator
{
    private const int MaxFileContentChars = 4000;

    public async Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating .context.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var keyFileContents = ReadKeyFiles(project.KeyFiles, repoPath);
        var userPrompt = BuildUserPrompt(project, keyFileContents, snapshot);

        var response = await llmClient.CompleteAsync(
            SystemPrompt, userPrompt, TaskType.ContextGeneration, cancellationToken);

        var yaml = LlmResponseHelper.StripCodeFences(response);

        logger.LogInformation("Generated .context.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    public async Task<string> RetryWithErrorsAsync(
        DetectedProject project,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> validationErrors,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrying .context.yaml generation with {ErrorCount} validation errors",
            validationErrors.Count);

        var errorList = string.Join('\n', validationErrors.Select(e => $"- {e}"));
        var retryPrompt = $"""
            The following .context.yaml was generated but has validation errors.
            Fix the errors and return only valid YAML.

            ## Validation Errors
            {errorList}

            ## Previous Output
            ```yaml
            {previousYaml}
            ```

            Return ONLY valid YAML, no explanation.
            """;

        var response = await llmClient.CompleteAsync(
            SystemPrompt, retryPrompt, TaskType.ContextGeneration, cancellationToken);

        return LlmResponseHelper.StripCodeFences(response);
    }

    internal static string ReadKeyFiles(IReadOnlyList<string> keyFiles, string repoPath)
    {
        var sections = new List<string>();

        foreach (var relativePath in keyFiles)
        {
            var fullPath = Path.Combine(repoPath, relativePath);
            if (!File.Exists(fullPath)) continue;

            try
            {
                var content = File.ReadAllText(fullPath);
                if (content.Length > MaxFileContentChars)
                    content = content[..MaxFileContentChars] + "\n... (truncated)";

                sections.Add($"### {relativePath}\n```\n{content}\n```");
            }
            catch (IOException) { }
        }

        return string.Join("\n\n", sections);
    }

    internal static string BuildUserPrompt(
        DetectedProject project,
        string keyFileContents,
        RepoSnapshot snapshot)
    {
        var detectedYaml = $"""
            language: {project.Language}
            runtime: {project.Runtime ?? "unknown"}
            package_manager: {project.PackageManager ?? "unknown"}
            build_command: {project.BuildCommand ?? "none"}
            test_command: {project.TestCommand ?? "none"}
            frameworks: [{string.Join(", ", project.Frameworks)}]
            infrastructure: [{string.Join(", ", project.Infrastructure)}]
            sdks: [{string.Join(", ", project.Sdks)}]
            """;

        var readmeSection = project.ReadmeExcerpt is not null
            ? $"""

            ## README (excerpt)
            {project.ReadmeExcerpt}
            """
            : "";

        var snapshotSection = BuildSnapshotSection(snapshot);
        var qualityTemplate = QualityTemplateExtended;

        const string emptyObj = "{}";

        return $"""
            ## Detected Stack
            {detectedYaml}

            ## Key Files
            {keyFileContents}
            {readmeSection}

            ## Directory Structure
            {snapshot.DirectoryTree}
            {snapshotSection}

            ## Template
            Use this exact structure for the output. Fill in what you can determine,
            leave out optional sections you cannot determine. Be precise, not verbose.

            ```yaml
            meta:
              project: <repo-name>
              version: 1.0.0
              type: [<archetypes>]
              purpose: "<one sentence>"

            stack:
              runtime: <runtime>
              lang: <language>
              infra: [<infrastructure>]
              testing: [<test-frameworks>]
              sdks: [<significant-sdks>]

            arch:
              style: [<architecture-style>]
              patterns: [<key-patterns>]
              layers:
                - <layer1>
                - <layer2>

            {qualityTemplate}

            state:
              done: {emptyObj}
              active: {emptyObj}
              planned: {emptyObj}
            ```

            Generate the .context.yaml. Return ONLY valid YAML, no explanation.
            """;
    }

    internal static string BuildSnapshotSection(RepoSnapshot snapshot)
    {
        var lines = new List<string>();

        if (snapshot.ConfigFileContents.Count > 0)
        {
            lines.Add("\n## Config Files (for style and tooling detection)");
            lines.AddRange(snapshot.ConfigFileContents);
        }

        if (snapshot.CodeSamples.Count > 0)
        {
            lines.Add("\n## Code Samples (for style, architecture, and pattern detection)");
            lines.AddRange(snapshot.CodeSamples);
        }

        return string.Join('\n', lines);
    }

    private const string QualityTemplateExtended = """
            quality:
              lang: english-only
              principles: [<detected-principles>]
              detected-style:
                naming: { classes: <PascalCase|camelCase|snake_case>, variables: <camelCase|snake_case>, files: <pattern> }
                indentation: { type: <spaces|tabs>, size: <n> }
                formatter: <name-or-none>
                linter: <name-or-none>
                pre-commit: <name-or-none>
              architecture:
                style: [<DDD|CleanArch|Hexagonal|MVC|Layered|ad-hoc>]
                patterns: [<CQRS|Repository|MediatR|Factory|Strategy|etc>]
                layer-discipline: <strict|loose|none>
                domain-model: <rich|anemic|none>
                di-approach: <constructor-injection|service-locator|framework-managed|none>
              methodology:
                testing: <test-first|test-after|no-tests>
                test-style: <AAA|GWT|BDD|unclear>
                coverage-estimate: <high|medium|low|none>
                ci-enforced: <true|false>
              quality-score: <high|medium|low>
              recommendation: <follow-existing|suggest-improvements>
            """;

    private const string SystemPrompt = """
        You are a project analyst. Generate a .context.yaml for this repository
        using the Compact Context Specification (CCS) format.

        Rules:
        - Use ONLY the template format provided
        - Fill in what you can determine from the files and structure
        - Leave out optional sections you cannot determine
        - Be precise, not verbose
        - meta.purpose must be one sentence, max 100 characters
        - Return ONLY valid YAML, no explanation or markdown fences
        """;
}

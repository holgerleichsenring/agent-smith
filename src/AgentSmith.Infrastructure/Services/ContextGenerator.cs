using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// Input: DetectedProject + key file contents + directory tree.
/// Output: raw YAML string.
/// </summary>
public sealed class ContextGenerator(
    string apiKey,
    RetryConfig retryConfig,
    ModelAssignment modelAssignment,
    ILogger<ContextGenerator> logger) : IContextGenerator
{
    private const int MaxFileContentChars = 4000;
    private const int MaxTreeDepth = 3;

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform"
    };

    public async Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating .context.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var keyFileContents = ReadKeyFiles(project.KeyFiles, repoPath);
        var directoryTree = GenerateTree(repoPath, MaxTreeDepth);
        var userPrompt = BuildUserPrompt(project, keyFileContents, directoryTree);

        using var client = CreateClient();

        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = modelAssignment.Model,
                MaxTokens = modelAssignment.MaxTokens,
                System = new List<SystemMessage> { new(SystemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
                    }
                },
                Stream = false
            },
            cancellationToken);

        var yaml = ExtractYaml(response);

        logger.LogInformation("Generated .context.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    public async Task<string> RetryWithErrorsAsync(
        DetectedProject project,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> validationErrors,
        CancellationToken cancellationToken = default)
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

        using var client = CreateClient();

        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = modelAssignment.Model,
                MaxTokens = modelAssignment.MaxTokens,
                System = new List<SystemMessage> { new(SystemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = retryPrompt } }
                    }
                },
                Stream = false
            },
            cancellationToken);

        return ExtractYaml(response);
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

    internal static string GenerateTree(string rootPath, int maxDepth)
    {
        var lines = new List<string>();
        BuildTreeLines(rootPath, "", maxDepth, 0, lines);
        return string.Join('\n', lines.Take(150));
    }

    private static void BuildTreeLines(
        string dirPath, string prefix, int maxDepth, int currentDepth, List<string> lines)
    {
        if (currentDepth >= maxDepth || lines.Count > 150) return;

        try
        {
            var entries = Directory.GetFileSystemEntries(dirPath)
                .Select(e => new { Path = e, Name = Path.GetFileName(e), IsDir = Directory.Exists(e) })
                .Where(e => !ExcludedDirs.Contains(e.Name))
                .OrderBy(e => !e.IsDir)
                .ThenBy(e => e.Name)
                .ToList();

            foreach (var entry in entries)
            {
                var marker = entry.IsDir ? "/" : "";
                lines.Add($"{prefix}{entry.Name}{marker}");

                if (entry.IsDir)
                    BuildTreeLines(entry.Path, prefix + "  ", maxDepth, currentDepth + 1, lines);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    internal static string BuildUserPrompt(
        DetectedProject project,
        string keyFileContents,
        string directoryTree)
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

        const string emptyObj = "{}";

        return $"""
            ## Detected Stack
            {detectedYaml}

            ## Key Files
            {keyFileContents}
            {readmeSection}

            ## Directory Structure
            {directoryTree}

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

            quality:
              lang: english-only
              principles: [<detected-principles>]

            state:
              done: {emptyObj}
              active: {emptyObj}
              planned: {emptyObj}
            ```

            Generate the .context.yaml. Return ONLY valid YAML, no explanation.
            """;
    }

    private AnthropicClient CreateClient()
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        return new AnthropicClient(apiKey, httpClient);
    }

    private static string ExtractYaml(MessageResponse response)
    {
        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";
        return StripCodeFences(text);
    }

    internal static string StripCodeFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```yaml", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

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

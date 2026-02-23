using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Generates a coding-principles.md for a repository using one cheap LLM call.
/// Analyzes code samples and config files to produce actionable coding guidelines.
/// </summary>
public sealed class CodingPrinciplesGenerator(
    string apiKey,
    RetryConfig retryConfig,
    ModelAssignment modelAssignment,
    ILogger<CodingPrinciplesGenerator> logger) : ICodingPrinciplesGenerator
{
    public async Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Generating coding-principles.md for {Lang} project at {Path}...",
            project.Language, repoPath);

        var userPrompt = BuildUserPrompt(project, snapshot);

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

        var markdown = response.Content.OfType<TextContent>().FirstOrDefault()?.Text?.Trim() ?? "";

        logger.LogInformation("Generated coding-principles.md ({Chars} chars)", markdown.Length);
        return markdown;
    }

    internal static string BuildUserPrompt(DetectedProject project, RepoSnapshot snapshot)
    {
        var configSection = snapshot.ConfigFileContents.Count > 0
            ? "## Config Files\n" + string.Join('\n', snapshot.ConfigFileContents)
            : "";

        var codeSection = snapshot.CodeSamples.Count > 0
            ? "## Code Samples\n" + string.Join('\n', snapshot.CodeSamples)
            : "";

        return $"""
            ## Project
            Language: {project.Language}
            Runtime: {project.Runtime ?? "unknown"}
            Frameworks: [{string.Join(", ", project.Frameworks)}]
            Build: {project.BuildCommand ?? "none"}
            Test: {project.TestCommand ?? "none"}

            {configSection}

            {codeSection}
            """;
    }

    private AnthropicClient CreateClient()
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        return new AnthropicClient(apiKey, httpClient);
    }

    private const string SystemPrompt = """
        You are a code style analyst. Generate a coding-principles.md for this repository.
        Analyze the config files and code samples to detect the project's actual conventions.

        Output a Markdown document with these sections:

        # Coding Principles

        ## Language
        State the primary language and required human language for all text (code, comments, docs).

        ## Naming Conventions
        Document the actual naming patterns found (classes, methods, variables, files, booleans, async methods, interfaces, fields).

        ## Code Style
        Indentation, max line length, formatter/linter used, file organization (one type per file, file-scoped namespaces, etc.).

        ## Architecture Patterns
        Detected patterns (DI, Repository, CQRS, Command/Handler, etc.) and layer structure if present.

        ## Testing
        Test framework, naming convention, style (AAA/GWT/BDD), what to mock.

        ## Hard Limits
        Any enforced limits (method length, class length, types per file) detected from config or consistent practice.

        Rules:
        - Only document what you can actually detect from the code and config
        - Be precise and actionable — an AI coding agent will follow these rules
        - Use imperative mood ("Use PascalCase", not "PascalCase is used")
        - Keep it concise — aim for 50-100 lines
        - Do NOT wrap in code fences — return plain Markdown
        """;
}

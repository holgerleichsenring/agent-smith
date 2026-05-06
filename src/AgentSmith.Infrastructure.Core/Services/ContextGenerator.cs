using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// </summary>
public sealed class ContextGenerator(
    ContextUserPromptBuilder userPromptBuilder,
    IPromptCatalog prompts,
    IChatClientFactory chatClientFactory,
    ILogger<ContextGenerator> logger) : IContextGenerator
{
    private const int MaxFileContentChars = 4000;

    public async Task<string> GenerateAsync(
        DetectedProject project, string repoPath, RepoSnapshot snapshot,
        AgentConfig agent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating .context.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var keyFileContents = ReadKeyFiles(project.KeyFiles, repoPath);
        var userPrompt = userPromptBuilder.Build(project, keyFileContents, snapshot);
        var systemPrompt = prompts.Get("context-generator-system");

        var text = await CallAsync(agent, systemPrompt, userPrompt, cancellationToken);
        var yaml = LlmResponseHelper.StripCodeFences(text);
        logger.LogInformation("Generated .context.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    public async Task<string> RetryWithErrorsAsync(
        DetectedProject project, string repoPath, string previousYaml,
        IReadOnlyList<string> validationErrors, AgentConfig agent,
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

        var systemPrompt = prompts.Get("context-generator-system");
        var text = await CallAsync(agent, systemPrompt, retryPrompt, cancellationToken);
        return LlmResponseHelper.StripCodeFences(text);
    }

    private async Task<string> CallAsync(
        AgentConfig agent, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var chat = chatClientFactory.Create(agent, TaskType.ContextGeneration);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.ContextGeneration);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, ct);
        return response.Text ?? string.Empty;
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
}

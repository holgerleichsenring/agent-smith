using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// </summary>
public sealed class ContextGenerator(
    ILogger<ContextGenerator> logger) : IContextGenerator
{
    private const int MaxFileContentChars = 4000;

    public async Task<string> GenerateAsync(
        DetectedProject project, string repoPath, RepoSnapshot snapshot,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating .context.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var keyFileContents = ReadKeyFiles(project.KeyFiles, repoPath);
        var userPrompt = ContextUserPromptBuilder.Build(project, keyFileContents, snapshot);

        var llmResponse = await llmClient.CompleteAsync(
            ContextPromptTemplates.SystemPrompt, userPrompt,
            TaskType.ContextGeneration, cancellationToken);

        var yaml = LlmResponseHelper.StripCodeFences(llmResponse.Text);
        logger.LogInformation("Generated .context.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    public async Task<string> RetryWithErrorsAsync(
        DetectedProject project, string repoPath, string previousYaml,
        IReadOnlyList<string> validationErrors, ILlmClient llmClient,
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

        var llmResponse = await llmClient.CompleteAsync(
            ContextPromptTemplates.SystemPrompt, retryPrompt,
            TaskType.ContextGeneration, cancellationToken);

        return LlmResponseHelper.StripCodeFences(llmResponse.Text);
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

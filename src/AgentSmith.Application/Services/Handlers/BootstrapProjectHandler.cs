using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Auto-detects project type and generates .agentsmith/ meta-files if not present.
/// Delegates meta-file generation to MetaFileBootstrapper.
/// </summary>
public sealed class BootstrapProjectHandler(
    IProjectDetector detector,
    IRepoSnapshotCollector snapshotCollector,
    ILlmClientFactory llmClientFactory,
    IContextGenerator generator,
    IContextValidator validator,
    MetaFileBootstrapper metaFileBootstrapper,
    ILogger<BootstrapProjectHandler> logger)
    : ICommandHandler<BootstrapProjectContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string ContextFileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapProjectContext context, CancellationToken cancellationToken)
    {
        var repoPath = context.Repository.LocalPath;
        var agentDir = Path.Combine(repoPath, AgentSmithDir);
        Directory.CreateDirectory(agentDir);

        var contextFilePath = Path.Combine(agentDir, ContextFileName);

        var detected = detector.Detect(repoPath);
        var snapshot = snapshotCollector.Collect(repoPath, detected);
        context.Pipeline.Set(ContextKeys.DetectedProject, detected);
        context.Pipeline.Set(ContextKeys.RepoSnapshot, snapshot);

        var llmClient = llmClientFactory.Create(context.Agent);
        var sourceType = context.Pipeline.TryGet<string>("SourceType", out var st) ? st : "github";

        if (File.Exists(contextFilePath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", ContextFileName);
            await metaFileBootstrapper.BootstrapAsync(
                detected, agentDir, repoPath, snapshot, llmClient,
                context.Pipeline, sourceType, cancellationToken);
            return CommandResult.Ok($"Existing {ContextFileName} found, project detected as {detected.Language}");
        }

        logger.LogInformation(
            "No {File} found. Generating for {Lang} project...",
            ContextFileName, detected.Language);

        var yaml = await GenerateContextYamlAsync(
            detected, repoPath, snapshot, llmClient, cancellationToken);

        if (yaml is null)
            return CommandResult.Fail($"Generated {ContextFileName} failed validation");

        await File.WriteAllTextAsync(contextFilePath, yaml, cancellationToken);
        logger.LogInformation("Written {File} ({Chars} chars)", contextFilePath, yaml.Length);

        await metaFileBootstrapper.BootstrapAsync(
            detected, agentDir, repoPath, snapshot, llmClient,
            context.Pipeline, sourceType, cancellationToken);

        return CommandResult.Ok(
            $"Generated {ContextFileName} for {detected.Language} project ({yaml.Length} chars)");
    }

    private async Task<string?> GenerateContextYamlAsync(
        DetectedProject detected, string repoPath, RepoSnapshot snapshot,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var yaml = await generator.GenerateAsync(detected, repoPath, snapshot, llmClient, cancellationToken);
        var validation = validator.Validate(yaml);

        if (validation.IsValid)
            return yaml;

        logger.LogWarning(
            "Generated YAML has {ErrorCount} validation errors, retrying...",
            validation.Errors.Count);

        yaml = await generator.RetryWithErrorsAsync(
            detected, repoPath, yaml, validation.Errors, llmClient, cancellationToken);
        validation = validator.Validate(yaml);

        if (validation.IsValid)
            return yaml;

        var errors = string.Join("; ", validation.Errors);
        logger.LogWarning("Retry also failed validation: {Errors}", errors);
        return null;
    }
}

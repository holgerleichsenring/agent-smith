using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
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
    IContextGenerator generator,
    IContextValidator validator,
    MetaFileBootstrapper metaFileBootstrapper,
    ISandboxFileReaderFactory readerFactory,
    ILogger<BootstrapProjectHandler> logger)
    : ICommandHandler<BootstrapProjectContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string ContextFileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapProjectContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var repoPath = context.Repository.LocalPath;
        var agentDir = Path.Combine(repoPath, AgentSmithDir);
        var contextFilePath = Path.Combine(agentDir, ContextFileName);

        logger.LogDebug("Bootstrap: repoPath={RepoPath}, agentDir={AgentDir}", repoPath, agentDir);

        var detected = await detector.DetectAsync(reader, repoPath, cancellationToken);
        var snapshot = await snapshotCollector.CollectAsync(reader, repoPath, detected, cancellationToken);
        context.Pipeline.Set(ContextKeys.DetectedProject, detected);
        context.Pipeline.Set(ContextKeys.RepoSnapshot, snapshot);

        var sourceType = context.Pipeline.TryGet<string>("SourceType", out var st) ? st ?? "github" : "github";
        logger.LogDebug("Bootstrap: agentType={Type}, sourceType={SourceType}, skillsPath={Skills}",
            context.Agent.Type, sourceType, context.SkillsPath);

        if (await reader.ExistsAsync(contextFilePath, cancellationToken))
        {
            logger.LogInformation("Found existing {File}, skipping generation", ContextFileName);
            await metaFileBootstrapper.BootstrapAsync(
                reader, detected, agentDir, repoPath, snapshot, context.Agent,
                context.Pipeline, sourceType, context.SkillsPath, cancellationToken);
            return CommandResult.Ok($"Existing {ContextFileName} found, project detected as {detected.Language}");
        }

        logger.LogInformation(
            "No {File} found. Generating for {Lang} project...",
            ContextFileName, detected.Language);

        var yaml = await GenerateContextYamlAsync(
            reader, detected, repoPath, snapshot, context.Agent, cancellationToken);

        if (yaml is null)
        {
            logger.LogWarning("Generated {File} failed validation, continuing without", ContextFileName);
        }
        else
        {
            await reader.WriteAsync(contextFilePath, yaml, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", contextFilePath, yaml.Length);
        }

        await metaFileBootstrapper.BootstrapAsync(
            reader, detected, agentDir, repoPath, snapshot, context.Agent,
            context.Pipeline, sourceType, context.SkillsPath, cancellationToken);

        return CommandResult.Ok(
            $"Generated {ContextFileName} for {detected.Language} project ({yaml?.Length ?? 0} chars)");
    }

    private async Task<string?> GenerateContextYamlAsync(
        ISandboxFileReader reader, DetectedProject detected, string repoPath, RepoSnapshot snapshot,
        AgentConfig agent, CancellationToken cancellationToken)
    {
        var yaml = await generator.GenerateAsync(reader, detected, repoPath, snapshot, agent, cancellationToken);
        var validation = validator.Validate(yaml);

        if (validation.IsValid)
            return yaml;

        logger.LogWarning(
            "Generated YAML has {ErrorCount} validation errors, retrying...",
            validation.Errors.Count);

        yaml = await generator.RetryWithErrorsAsync(
            detected, yaml, validation.Errors, agent, cancellationToken);
        validation = validator.Validate(yaml);

        if (validation.IsValid)
            return yaml;

        var errors = string.Join("; ", validation.Errors);
        logger.LogWarning("Retry also failed validation: {Errors}", errors);
        return null;
    }
}

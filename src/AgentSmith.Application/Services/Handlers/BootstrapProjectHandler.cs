using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Auto-detects project type and generates .context.yaml and code-map.yaml if not present.
/// Stores DetectedProject in pipeline context for downstream use (e.g. TestHandler).
/// </summary>
public sealed class BootstrapProjectHandler(
    IProjectDetector detector,
    IRepoSnapshotCollector snapshotCollector,
    IContextGenerator generator,
    IContextValidator validator,
    ICodeMapGenerator codeMapGenerator,
    ILogger<BootstrapProjectHandler> logger)
    : ICommandHandler<BootstrapProjectContext>
{
    private const string ContextFileName = ".context.yaml";
    private const string CodeMapFileName = "code-map.yaml";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapProjectContext context, CancellationToken cancellationToken = default)
    {
        var repoPath = context.Repository.LocalPath;
        var contextFilePath = Path.Combine(repoPath, ContextFileName);

        // Always detect project and collect snapshot for pipeline context
        var detected = detector.Detect(repoPath);
        var snapshot = snapshotCollector.Collect(repoPath, detected);
        context.Pipeline.Set(ContextKeys.DetectedProject, detected);
        context.Pipeline.Set(ContextKeys.RepoSnapshot, snapshot);

        if (File.Exists(contextFilePath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", ContextFileName);
            await TryGenerateCodeMapAsync(detected, repoPath, cancellationToken);
            return CommandResult.Ok($"Existing {ContextFileName} found, project detected as {detected.Language}");
        }

        logger.LogInformation(
            "No {File} found. Generating for {Lang} project...",
            ContextFileName, detected.Language);

        var yaml = await generator.GenerateAsync(detected, repoPath, snapshot, cancellationToken);
        var validation = validator.Validate(yaml);

        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Generated YAML has {ErrorCount} validation errors, retrying...",
                validation.Errors.Count);

            yaml = await RetryGenerationAsync(detected, repoPath, yaml, validation.Errors, cancellationToken);
            validation = validator.Validate(yaml);

            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors);
                logger.LogWarning("Retry also failed validation: {Errors}", errors);
                return CommandResult.Fail($"Generated {ContextFileName} failed validation: {errors}");
            }
        }

        await File.WriteAllTextAsync(contextFilePath, yaml, cancellationToken);
        logger.LogInformation("Written {File} ({Chars} chars)", contextFilePath, yaml.Length);

        await TryGenerateCodeMapAsync(detected, repoPath, cancellationToken);

        return CommandResult.Ok(
            $"Generated {ContextFileName} for {detected.Language} project ({yaml.Length} chars)");
    }

    private async Task TryGenerateCodeMapAsync(
        DetectedProject detected, string repoPath, CancellationToken cancellationToken)
    {
        var codeMapPath = Path.Combine(repoPath, CodeMapFileName);

        if (File.Exists(codeMapPath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", CodeMapFileName);
            return;
        }

        try
        {
            logger.LogInformation("Generating {File} for {Lang} project...", CodeMapFileName, detected.Language);
            var codeMap = await codeMapGenerator.GenerateAsync(detected, repoPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(codeMap))
            {
                logger.LogWarning("Code map generation returned empty result, skipping");
                return;
            }

            await File.WriteAllTextAsync(codeMapPath, codeMap, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", codeMapPath, codeMap.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Code map generation failed, continuing without code map");
        }
    }

    private async Task<string> RetryGenerationAsync(
        DetectedProject detected,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        return await generator.RetryWithErrorsAsync(
            detected, repoPath, previousYaml, errors, cancellationToken);
    }
}

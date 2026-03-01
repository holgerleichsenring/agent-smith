using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Auto-detects project type and generates .agentsmith/ meta-files if not present:
/// context.yaml, code-map.yaml, and coding-principles.md.
/// Creates a per-project ILlmClient via ILlmClientFactory for generator calls.
/// Stores DetectedProject in pipeline context for downstream use (e.g. TestHandler).
/// </summary>
public sealed class BootstrapProjectHandler(
    IProjectDetector detector,
    IRepoSnapshotCollector snapshotCollector,
    ILlmClientFactory llmClientFactory,
    IContextGenerator generator,
    IContextValidator validator,
    ICodeMapGenerator codeMapGenerator,
    ICodingPrinciplesGenerator codingPrinciplesGenerator,
    ISkillLoader skillLoader,
    ILogger<BootstrapProjectHandler> logger)
    : ICommandHandler<BootstrapProjectContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string ContextFileName = "context.yaml";
    private const string CodeMapFileName = "code-map.yaml";
    private const string CodingPrinciplesFileName = "coding-principles.md";
    private const string SkillFileName = "skill.yaml";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapProjectContext context, CancellationToken cancellationToken)
    {
        var repoPath = context.Repository.LocalPath;
        var agentDir = Path.Combine(repoPath, AgentSmithDir);
        Directory.CreateDirectory(agentDir);

        var contextFilePath = Path.Combine(agentDir, ContextFileName);

        // Always detect project and collect snapshot for pipeline context
        var detected = detector.Detect(repoPath);
        var snapshot = snapshotCollector.Collect(repoPath, detected);
        context.Pipeline.Set(ContextKeys.DetectedProject, detected);
        context.Pipeline.Set(ContextKeys.RepoSnapshot, snapshot);

        // Create per-project LLM client from agent configuration
        var llmClient = llmClientFactory.Create(context.Agent);

        if (File.Exists(contextFilePath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", ContextFileName);
            await TryGenerateCodeMapAsync(detected, agentDir, repoPath, snapshot, llmClient, cancellationToken);
            await TryGenerateCodingPrinciplesAsync(detected, agentDir, repoPath, snapshot, llmClient, cancellationToken);
            TryGenerateSkillYaml(detected, agentDir, context);
            TryLoadSkillRoles(agentDir, context.Pipeline);
            return CommandResult.Ok($"Existing {ContextFileName} found, project detected as {detected.Language}");
        }

        logger.LogInformation(
            "No {File} found. Generating for {Lang} project...",
            ContextFileName, detected.Language);

        var yaml = await generator.GenerateAsync(detected, repoPath, snapshot, llmClient, cancellationToken);
        var validation = validator.Validate(yaml);

        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Generated YAML has {ErrorCount} validation errors, retrying...",
                validation.Errors.Count);

            yaml = await RetryGenerationAsync(detected, repoPath, yaml, validation.Errors, llmClient, cancellationToken);
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

        await TryGenerateCodeMapAsync(detected, agentDir, repoPath, snapshot, llmClient, cancellationToken);
        await TryGenerateCodingPrinciplesAsync(detected, agentDir, repoPath, snapshot, llmClient, cancellationToken);
        TryGenerateSkillYaml(detected, agentDir, context);
        TryLoadSkillRoles(agentDir, context.Pipeline);

        return CommandResult.Ok(
            $"Generated {ContextFileName} for {detected.Language} project ({yaml.Length} chars)");
    }

    private async Task TryGenerateCodeMapAsync(
        DetectedProject detected, string agentDir, string repoPath,
        RepoSnapshot snapshot, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var codeMapPath = Path.Combine(agentDir, CodeMapFileName);

        if (File.Exists(codeMapPath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", CodeMapFileName);
            return;
        }

        try
        {
            logger.LogInformation("Generating {File} for {Lang} project...", CodeMapFileName, detected.Language);
            var codeMap = await codeMapGenerator.GenerateAsync(detected, repoPath, snapshot, llmClient, cancellationToken);

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

    private async Task TryGenerateCodingPrinciplesAsync(
        DetectedProject detected, string agentDir, string repoPath,
        RepoSnapshot snapshot, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var principlesPath = Path.Combine(agentDir, CodingPrinciplesFileName);

        if (File.Exists(principlesPath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", CodingPrinciplesFileName);
            return;
        }

        try
        {
            logger.LogInformation("Generating {File} for {Lang} project...", CodingPrinciplesFileName, detected.Language);
            var principles = await codingPrinciplesGenerator.GenerateAsync(
                detected, repoPath, snapshot, llmClient, cancellationToken);

            if (string.IsNullOrWhiteSpace(principles))
            {
                logger.LogWarning("Coding principles generation returned empty result, skipping");
                return;
            }

            await File.WriteAllTextAsync(principlesPath, principles, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", principlesPath, principles.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Coding principles generation failed, continuing without");
        }
    }

    private void TryGenerateSkillYaml(
        DetectedProject detected, string agentDir, BootstrapProjectContext context)
    {
        var skillPath = Path.Combine(agentDir, SkillFileName);

        if (File.Exists(skillPath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", SkillFileName);
            return;
        }

        try
        {
            var yaml = GenerateSkillYaml(detected, context);
            File.WriteAllText(skillPath, yaml);
            logger.LogInformation("Written {File} ({Chars} chars)", skillPath, yaml.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skill YAML generation failed, continuing without");
        }
    }

    private void TryLoadSkillRoles(string agentDir, PipelineContext pipeline)
    {
        try
        {
            var allRoles = skillLoader.LoadRoleDefinitions("config/skills");
            if (allRoles.Count == 0)
            {
                logger.LogDebug("No role definitions found in config/skills, skipping skill loading");
                return;
            }

            var projectSkills = skillLoader.LoadProjectSkills(agentDir);

            IReadOnlyList<RoleSkillDefinition> activeRoles;
            if (projectSkills is not null)
            {
                activeRoles = skillLoader.GetActiveRoles(allRoles, projectSkills);
                pipeline.Set(ContextKeys.ProjectSkills, projectSkills);
                logger.LogInformation(
                    "Loaded {Active}/{Total} roles (filtered by skill.yaml)",
                    activeRoles.Count, allRoles.Count);
            }
            else
            {
                activeRoles = allRoles;
                logger.LogInformation(
                    "Loaded {Count} roles (no skill.yaml, all available for triage)",
                    allRoles.Count);
            }

            if (activeRoles.Count > 0)
                pipeline.Set(ContextKeys.AvailableRoles, activeRoles);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load skill roles, continuing without skills");
        }
    }

    private static string GenerateSkillYaml(DetectedProject detected, BootstrapProjectContext context)
    {
        var allItems = detected.Frameworks
            .Concat(detected.Infrastructure)
            .Concat(detected.Sdks)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();

        var hasBackend = allItems.Any(s =>
            s.Contains("asp.net") || s.Contains("spring") || s.Contains("django") ||
            s.Contains("express") || s.Contains("flask") || s.Contains("rails") ||
            s.Contains("fastapi") || s.Contains("dotnet") || s.Contains(".net"));

        var hasFrontend = allItems.Any(s =>
            s.Contains("react") || s.Contains("angular") || s.Contains("vue") ||
            s.Contains("blazor") || s.Contains("next") || s.Contains("nuxt") ||
            s.Contains("svelte"));

        var hasInfra = allItems.Any(s =>
            s.Contains("docker") || s.Contains("kubernetes") || s.Contains("k8s") ||
            s.Contains("terraform") || s.Contains("helm") || s.Contains("ci") ||
            s.Contains("cd"));

        var hasDatabase = allItems.Any(s =>
            s.Contains("sql") || s.Contains("postgres") || s.Contains("mongo") ||
            s.Contains("redis") || s.Contains("entity framework") || s.Contains("dapper") ||
            s.Contains("prisma") || s.Contains("sequelize"));

        var hasTesting = allItems.Any(s =>
            s.Contains("xunit") || s.Contains("nunit") || s.Contains("mstest") ||
            s.Contains("jest") || s.Contains("mocha") || s.Contains("pytest") ||
            s.Contains("junit"));

        // If neither backend nor frontend detected, default to backend
        if (!hasBackend && !hasFrontend)
            hasBackend = true;

        var sourceType = context.Pipeline.TryGet<string>("SourceType", out var st) ? st : "github";

        return $"""
            # Auto-generated by 'agentsmith init' based on project analysis.
            # Customize as needed.

            input:
              type: ticket
              provider: {sourceType}

            output:
              type: pull-request
              provider: {sourceType}

            context:
              rules: coding-principles.md
              map: code-map.yaml

            roles:
              architect:
                enabled: true
              backend-developer:
                enabled: {(hasBackend ? "true" : "false")}
              frontend-developer:
                enabled: {(hasFrontend ? "true" : "false")}
              devops:
                enabled: {(hasInfra ? "true" : "false")}
              tester:
                enabled: {(hasTesting ? "true" : "false")}
              dba:
                enabled: {(hasDatabase ? "true" : "false")}
              product-owner:
                enabled: false
              security-reviewer:
                enabled: false

            discussion:
              max_rounds: 3
              max_total_commands: 50
              convergence_threshold: 0
            """;
    }

    private async Task<string> RetryGenerationAsync(
        DetectedProject detected,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> errors,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        return await generator.RetryWithErrorsAsync(
            detected, repoPath, previousYaml, errors, llmClient, cancellationToken);
    }
}

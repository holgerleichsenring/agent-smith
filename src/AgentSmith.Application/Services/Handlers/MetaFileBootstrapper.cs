using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates optional .agentsmith/ meta-files (code-map, coding-principles, skill.yaml)
/// and loads skill role definitions into the pipeline context. Sandbox-routed via
/// ISandboxFileReader. Each generation is best-effort: failures log and continue.
/// </summary>
public sealed class MetaFileBootstrapper(
    ICodeMapGenerator codeMapGenerator,
    ICodingPrinciplesGenerator codingPrinciplesGenerator,
    ISkillLoader skillLoader,
    ILogger<MetaFileBootstrapper> logger)
{
    private const string CodeMapFileName = "code-map.yaml";
    private const string CodingPrinciplesFileName = "coding-principles.md";
    private const string SkillFileName = "skill.yaml";
    private const string DecisionsFileName = "decisions.md";

    public async Task BootstrapAsync(
        ISandboxFileReader reader, DetectedProject detected, string agentDir, string repoPath,
        RepoSnapshot snapshot, AgentConfig agent,
        PipelineContext pipeline, string sourceType, string skillsPath,
        CancellationToken cancellationToken)
    {
        await TryGenerateFileAsync(reader, CodeMapFileName, agentDir, detected,
            (d, s, a, ct) => codeMapGenerator.GenerateAsync(d, repoPath, s, a, ct),
            snapshot, agent, cancellationToken);

        await TryGenerateFileAsync(reader, CodingPrinciplesFileName, agentDir, detected,
            (d, s, a, ct) => codingPrinciplesGenerator.GenerateAsync(d, repoPath, s, a, ct),
            snapshot, agent, cancellationToken);

        await TryGenerateSkillYamlAsync(reader, detected, agentDir, sourceType, cancellationToken);
        await TryCreateDecisionsTemplateAsync(reader, agentDir, cancellationToken);
        TryLoadSkillRoles(agentDir, skillsPath, pipeline);
    }

    private async Task TryGenerateFileAsync(
        ISandboxFileReader reader, string fileName, string agentDir, DetectedProject detected,
        Func<DetectedProject, RepoSnapshot, AgentConfig, CancellationToken, Task<string>> generate,
        RepoSnapshot snapshot, AgentConfig agent, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(agentDir, fileName);
        if (await reader.ExistsAsync(filePath, cancellationToken))
        {
            logger.LogInformation("Found existing {File}, skipping generation", fileName);
            return;
        }

        try
        {
            logger.LogInformation("Generating {File} for {Lang} project...", fileName, detected.Language);
            var content = await generate(detected, snapshot, agent, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("{File} generation returned empty result, skipping", fileName);
                return;
            }

            await reader.WriteAsync(filePath, content, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", filePath, content.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{File} generation failed, continuing without", fileName);
        }
    }

    private async Task TryGenerateSkillYamlAsync(
        ISandboxFileReader reader, DetectedProject detected, string agentDir, string sourceType,
        CancellationToken cancellationToken)
    {
        var skillPath = Path.Combine(agentDir, SkillFileName);
        if (await reader.ExistsAsync(skillPath, cancellationToken))
        {
            logger.LogInformation("Found existing {File}, skipping generation", SkillFileName);
            return;
        }

        try
        {
            var yaml = SkillYamlGenerator.Generate(detected, sourceType);
            await reader.WriteAsync(skillPath, yaml, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", skillPath, yaml.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skill YAML generation failed, continuing without");
        }
    }

    private async Task TryCreateDecisionsTemplateAsync(
        ISandboxFileReader reader, string agentDir, CancellationToken cancellationToken)
    {
        var path = Path.Combine(agentDir, DecisionsFileName);
        if (await reader.ExistsAsync(path, cancellationToken))
        {
            logger.LogInformation("Found existing {File}, skipping creation", DecisionsFileName);
            return;
        }

        try
        {
            await reader.WriteAsync(path, "# Decision Log\n", cancellationToken);
            logger.LogInformation("Created empty {File}", DecisionsFileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Decision log creation failed, continuing without");
        }
    }

    private void TryLoadSkillRoles(string agentDir, string skillsPath, PipelineContext pipeline)
    {
        try
        {
            var allRoles = skillLoader.LoadRoleDefinitions(skillsPath);
            if (allRoles.Count == 0) return;

            var projectSkills = skillLoader.LoadProjectSkills(agentDir);
            var activeRoles = allRoles;

            if (projectSkills is not null)
            {
                var filtered = skillLoader.GetActiveRoles(allRoles, projectSkills);
                if (filtered.Count > 0)
                {
                    activeRoles = filtered;
                    pipeline.Set(ContextKeys.ProjectSkills, projectSkills);
                }
            }

            if (activeRoles.Count > 0)
                pipeline.Set(ContextKeys.AvailableRoles, activeRoles);

            logger.LogInformation("Loaded {Active}/{Total} skill roles", activeRoles.Count, allRoles.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load skill roles, continuing without skills");
        }
    }
}

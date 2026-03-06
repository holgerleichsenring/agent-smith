using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates optional .agentsmith/ meta-files (code-map, coding-principles, skill.yaml)
/// and loads skill role definitions into the pipeline context.
/// Each generation is best-effort: failures are logged but do not block the pipeline.
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

    public async Task BootstrapAsync(
        DetectedProject detected, string agentDir, string repoPath,
        RepoSnapshot snapshot, ILlmClient llmClient,
        PipelineContext pipeline, string sourceType, string skillsPath,
        CancellationToken cancellationToken)
    {
        await TryGenerateFileAsync(CodeMapFileName, agentDir, detected,
            (d, s, c, ct) => codeMapGenerator.GenerateAsync(d, repoPath, s, c, ct),
            snapshot, llmClient, cancellationToken);

        await TryGenerateFileAsync(CodingPrinciplesFileName, agentDir, detected,
            (d, s, c, ct) => codingPrinciplesGenerator.GenerateAsync(d, repoPath, s, c, ct),
            snapshot, llmClient, cancellationToken);

        TryGenerateSkillYaml(detected, agentDir, sourceType);
        TryLoadSkillRoles(agentDir, skillsPath, pipeline);
    }

    private async Task TryGenerateFileAsync(
        string fileName, string agentDir, DetectedProject detected,
        Func<DetectedProject, RepoSnapshot, ILlmClient, CancellationToken, Task<string>> generate,
        RepoSnapshot snapshot, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(agentDir, fileName);
        if (File.Exists(filePath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", fileName);
            return;
        }

        try
        {
            logger.LogInformation("Generating {File} for {Lang} project...", fileName, detected.Language);
            var content = await generate(detected, snapshot, llmClient, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("{File} generation returned empty result, skipping", fileName);
                return;
            }

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogInformation("Written {File} ({Chars} chars)", filePath, content.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{File} generation failed, continuing without", fileName);
        }
    }

    private void TryGenerateSkillYaml(
        DetectedProject detected, string agentDir, string sourceType)
    {
        var skillPath = Path.Combine(agentDir, SkillFileName);
        if (File.Exists(skillPath))
        {
            logger.LogInformation("Found existing {File}, skipping generation", SkillFileName);
            return;
        }

        try
        {
            var yaml = SkillYamlGenerator.Generate(detected, sourceType);
            File.WriteAllText(skillPath, yaml);
            logger.LogInformation("Written {File} ({Chars} chars)", skillPath, yaml.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skill YAML generation failed, continuing without");
        }
    }

    private void TryLoadSkillRoles(string agentDir, string skillsPath, PipelineContext pipeline)
    {
        try
        {
            var allRoles = skillLoader.LoadRoleDefinitions(skillsPath);
            if (allRoles.Count == 0) return;

            var projectSkills = skillLoader.LoadProjectSkills(agentDir);
            var activeRoles = projectSkills is not null
                ? skillLoader.GetActiveRoles(allRoles, projectSkills)
                : allRoles;

            if (projectSkills is not null)
                pipeline.Set(ContextKeys.ProjectSkills, projectSkills);

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

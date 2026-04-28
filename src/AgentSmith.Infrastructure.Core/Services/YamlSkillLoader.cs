using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Loads skill configuration and role definitions from SKILL.md directories or legacy YAML files.
/// </summary>
public sealed class YamlSkillLoader(
    ISkillsCatalogPath catalogPath,
    ILogger<YamlSkillLoader> logger) : ISkillLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly SkillMdParser _skillMdParser = new(logger);

    public SkillConfig? LoadProjectSkills(string agentSmithDirectory)
    {
        var path = Path.Combine(agentSmithDirectory, "skill.yaml");

        if (!File.Exists(path))
        {
            logger.LogDebug("No skill.yaml found at {Path}, using single-skill mode", path);
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(path);
            var config = Deserializer.Deserialize<SkillConfig>(yaml);
            logger.LogInformation(
                "Loaded skill config with {RoleCount} roles from {Path}",
                config?.Roles.Count ?? 0, path);
            return config;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load skill.yaml from {Path}", path);
            return null;
        }
    }

    public IReadOnlyList<RoleSkillDefinition> LoadRoleDefinitions(string skillsDirectory)
    {
        var resolved = ResolveDirectory(skillsDirectory);
        if (!Directory.Exists(resolved))
        {
            logger.LogDebug("Skills directory not found: {Path}", resolved);
            return [];
        }

        var roles = new List<RoleSkillDefinition>();
        LoadFromSkillMdDirectories(resolved, roles);
        LoadFromLegacyYaml(resolved, roles);

        logger.LogInformation("Loaded {Count} role definitions from {Path}", roles.Count, resolved);
        return roles;
    }

    public IReadOnlyList<RoleSkillDefinition> GetActiveRoles(
        IReadOnlyList<RoleSkillDefinition> allRoles,
        SkillConfig projectSkills)
    {
        var activeRoles = new List<RoleSkillDefinition>();

        foreach (var role in allRoles)
        {
            if (!projectSkills.Roles.TryGetValue(role.Name, out var projectConfig))
                continue;

            if (!projectConfig.Enabled)
                continue;

            if (!string.IsNullOrEmpty(projectConfig.ExtraRules))
            {
                activeRoles.Add(new RoleSkillDefinition
                {
                    Name = role.Name,
                    DisplayName = role.DisplayName,
                    Emoji = role.Emoji,
                    Description = role.Description,
                    Triggers = role.Triggers,
                    Rules = $"{role.Rules}\n\n## Project-Specific Rules\n{projectConfig.ExtraRules}",
                    ConvergenceCriteria = role.ConvergenceCriteria,
                    Source = role.Source,
                    Orchestration = role.Orchestration
                });
            }
            else
            {
                activeRoles.Add(role);
            }
        }

        return activeRoles;
    }

    private void LoadFromSkillMdDirectories(string skillsDirectory, List<RoleSkillDefinition> roles)
    {
        foreach (var dir in Directory.GetDirectories(skillsDirectory).OrderBy(d => d))
        {
            if (!File.Exists(Path.Combine(dir, "SKILL.md")))
                continue;

            try
            {
                var role = _skillMdParser.Parse(dir);
                if (role is not null && !string.IsNullOrEmpty(role.Name))
                {
                    roles.Add(role);
                    logger.LogDebug("Loaded role definition from SKILL.md: {Name}", role.Name);
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Invalid skill configuration in {Dir} — skill not loaded", dir);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load role definition from {Dir}", dir);
            }
        }
    }

    private void LoadFromLegacyYaml(string skillsDirectory, List<RoleSkillDefinition> roles)
    {
        foreach (var file in Directory.GetFiles(skillsDirectory, "*.yaml").OrderBy(f => f))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var role = Deserializer.Deserialize<RoleSkillDefinition>(yaml);

                if (role is not null && !string.IsNullOrEmpty(role.Name))
                {
                    roles.Add(role);
                    logger.LogDebug("Loaded role definition from YAML: {Name}", role.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load role definition from {File}", file);
            }
        }
    }

    private string ResolveDirectory(string skillsDirectory)
    {
        if (string.IsNullOrWhiteSpace(skillsDirectory))
            return skillsDirectory;
        if (Path.IsPathRooted(skillsDirectory))
            return skillsDirectory;

        try
        {
            return Path.Combine(catalogPath.Root, skillsDirectory);
        }
        catch (InvalidOperationException)
        {
            // Bootstrap hasn't run yet (e.g. CLI tooling that bypasses the server lifecycle).
            return skillsDirectory;
        }
    }
}

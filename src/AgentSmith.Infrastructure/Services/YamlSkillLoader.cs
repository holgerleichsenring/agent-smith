using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Loads skill configuration and role definitions from YAML files.
/// </summary>
public sealed class YamlSkillLoader(ILogger<YamlSkillLoader> logger) : ISkillLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

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
        if (!Directory.Exists(skillsDirectory))
        {
            logger.LogDebug("Skills directory not found: {Path}", skillsDirectory);
            return [];
        }

        var roles = new List<RoleSkillDefinition>();

        foreach (var file in Directory.GetFiles(skillsDirectory, "*.yaml").OrderBy(f => f))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var role = Deserializer.Deserialize<RoleSkillDefinition>(yaml);

                if (role is not null && !string.IsNullOrEmpty(role.Name))
                {
                    roles.Add(role);
                    logger.LogDebug("Loaded role definition: {Name}", role.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load role definition from {File}", file);
            }
        }

        logger.LogInformation("Loaded {Count} role definitions from {Path}", roles.Count, skillsDirectory);
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
                    ConvergenceCriteria = role.ConvergenceCriteria
                });
            }
            else
            {
                activeRoles.Add(role);
            }
        }

        return activeRoles;
    }
}

using System.Text.RegularExpressions;
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
public sealed class YamlSkillLoader(ILogger<YamlSkillLoader> logger) : ISkillLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly IDeserializer FrontmatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
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

        // Load from SKILL.md directories (new format)
        foreach (var dir in Directory.GetDirectories(skillsDirectory).OrderBy(d => d))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMdPath))
                continue;

            try
            {
                var role = LoadFromSkillMd(dir);
                if (role is not null && !string.IsNullOrEmpty(role.Name))
                {
                    roles.Add(role);
                    logger.LogDebug("Loaded role definition from SKILL.md: {Name}", role.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load role definition from {Dir}", dir);
            }
        }

        // Load from legacy YAML files (backward compatibility)
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
                    ConvergenceCriteria = role.ConvergenceCriteria,
                    Source = role.Source
                });
            }
            else
            {
                activeRoles.Add(role);
            }
        }

        return activeRoles;
    }

    private RoleSkillDefinition? LoadFromSkillMd(string skillDirectory)
    {
        var skillMdPath = Path.Combine(skillDirectory, "SKILL.md");
        var content = File.ReadAllText(skillMdPath);

        var (frontmatter, body) = ParseFrontmatter(content);
        if (string.IsNullOrWhiteSpace(frontmatter))
        {
            logger.LogWarning("No frontmatter found in {Path}", skillMdPath);
            return null;
        }

        var meta = FrontmatterDeserializer.Deserialize<SkillMdFrontmatter>(frontmatter);
        if (meta is null || string.IsNullOrEmpty(meta.Name))
            return null;

        var role = new RoleSkillDefinition
        {
            Name = meta.Name,
            DisplayName = meta.DisplayName ?? string.Empty,
            Emoji = meta.Emoji ?? string.Empty,
            Description = meta.Description ?? string.Empty,
            Triggers = meta.Triggers ?? [],
            Rules = body.Trim()
        };

        // Load agentsmith.md (convergence criteria)
        var agentSmithPath = Path.Combine(skillDirectory, "agentsmith.md");
        if (File.Exists(agentSmithPath))
        {
            var agentSmithContent = File.ReadAllText(agentSmithPath);
            role.ConvergenceCriteria = ParseConvergenceCriteria(agentSmithContent);
        }

        // Load source.md (provenance)
        var sourcePath = Path.Combine(skillDirectory, "source.md");
        if (File.Exists(sourcePath))
        {
            role.Source = ParseSource(sourcePath);
        }

        return role;
    }

    private static (string frontmatter, string body) ParseFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return (string.Empty, content);

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (string.Empty, content);

        var frontmatter = content[4..endIndex].Trim();
        var body = content[(endIndex + 4)..];
        return (frontmatter, body);
    }

    private static List<string> ParseConvergenceCriteria(string content)
    {
        var criteria = new List<string>();
        var inSection = false;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("## convergence_criteria", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection && trimmed.StartsWith("## "))
                break;

            if (inSection && trimmed.StartsWith("- "))
            {
                var value = trimmed[2..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(value))
                    criteria.Add(value);
            }
        }

        return criteria;
    }

    private SkillSource? ParseSource(string sourcePath)
    {
        try
        {
            var content = File.ReadAllText(sourcePath);
            var origin = ExtractField(content, "origin");
            var version = ExtractField(content, "version");
            var commit = ExtractField(content, "commit");
            var reviewedStr = ExtractField(content, "reviewed");
            var reviewedBy = ExtractField(content, "reviewed-by");

            if (string.IsNullOrEmpty(origin))
                return null;

            var reviewed = DateOnly.TryParse(reviewedStr, out var date) ? date : DateOnly.MinValue;
            return new SkillSource(origin, version, commit, reviewed, reviewedBy);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse source.md from {Path}", sourcePath);
            return null;
        }
    }

    private static string ExtractField(string content, string fieldName)
    {
        var pattern = $@"^{Regex.Escape(fieldName)}:\s*(.+)$";
        var match = Regex.Match(content, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    /// <summary>
    /// Frontmatter model for SKILL.md files using hyphenated naming convention.
    /// </summary>
    private sealed class SkillMdFrontmatter
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Emoji { get; set; }
        public string? Description { get; set; }
        public List<string>? Triggers { get; set; }
        public string? Version { get; set; }
        public string? AllowedTools { get; set; }
    }
}

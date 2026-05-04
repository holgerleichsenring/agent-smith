using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Loads skill configuration and role definitions from SKILL.md directories or legacy YAML files.
/// Strict-validates SKILL.md frontmatter (roles_supported, role bodies, references uniqueness)
/// and aggregates per-category index files via SkillIndexBuilder.
/// </summary>
public sealed class YamlSkillLoader(
    ISkillsCatalogPath catalogPath,
    ConceptVocabularyLoader vocabularyLoader,
    ConceptVocabularyValidator vocabularyValidator,
    SkillIndexBuilder indexBuilder,
    IProviderOverrideResolver overrideResolver,
    ILogger<YamlSkillLoader> logger) : ISkillLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly SkillMdParser _skillMdParser = new(overrideResolver, logger);

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

        var vocabulary = vocabularyLoader.Load(FindVocabularyDirectory(resolved));
        vocabularyValidator.Validate(roles, vocabulary);
        indexBuilder.Build(resolved, roles);

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
                    Orchestration = role.Orchestration,
                    RolesSupported = role.RolesSupported,
                    Activation = role.Activation,
                    RoleAssignments = role.RoleAssignments,
                    References = role.References,
                    OutputContract = role.OutputContract,
                    RoleBodies = role.RoleBodies,
                    SkillDirectory = role.SkillDirectory
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
            if (Path.GetFileName(dir).StartsWith('_')) continue; // skip _index/ etc.
            if (!File.Exists(Path.Combine(dir, "SKILL.md")))
                continue;

            try
            {
                var role = _skillMdParser.Parse(dir);
                if (role is null || string.IsNullOrEmpty(role.Name))
                    continue;

                if (!ValidateStrict(role, dir, out var error))
                {
                    logger.LogError(
                        "Skill '{Skill}' at {Dir} rejected: {Error}. See docs/configuration/skills/migration.md",
                        role.Name, dir, error);
                    continue;
                }

                roles.Add(role);
                logger.LogDebug("Loaded role definition from SKILL.md: {Name}", role.Name);
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

    /// <summary>
    /// concept-vocabulary.yaml lives at the skills repo root (one or two levels above a per-category
    /// skillsDirectory). Walk up at most three levels to find it; fall back to skillsDirectory if absent.
    /// </summary>
    private static string FindVocabularyDirectory(string skillsDirectory)
    {
        var current = skillsDirectory;
        for (var depth = 0; depth < 3 && !string.IsNullOrEmpty(current); depth++)
        {
            if (File.Exists(Path.Combine(current, "concept-vocabulary.yaml")))
                return current;
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
        return skillsDirectory;
    }

    private static bool ValidateStrict(RoleSkillDefinition skill, string source, out string error)
    {
        if (skill.RolesSupported is null || skill.RolesSupported.Count == 0)
        {
            error = "frontmatter is missing 'roles_supported' — every skill must declare which roles it can take";
            return false;
        }

        if (skill.RoleBodies is null || skill.RoleBodies.Count == 0)
        {
            error = "body has no '## as_<role>' sections — every skill with roles_supported must split body per role";
            return false;
        }

        foreach (var role in skill.RolesSupported)
        {
            if (!skill.RoleBodies.ContainsKey(role))
            {
                error = $"declares role '{role}' but body has no '## as_{role.ToString().ToLowerInvariant()}' section";
                return false;
            }
        }

        if (skill.RoleAssignments is not null)
        {
            var supported = skill.RolesSupported.ToHashSet();
            foreach (var ra in skill.RoleAssignments)
            {
                if (!supported.Contains(ra.Role))
                {
                    error = $"role_assignment declares role '{ra.Role}' which is not in roles_supported";
                    return false;
                }
            }
        }

        if (skill.References is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in skill.References)
            {
                if (!seen.Add(r.Id))
                {
                    error = $"references[] has duplicate id '{r.Id}'";
                    return false;
                }
            }
        }

        _ = source; // reserved for future error reporting that includes the source path
        error = string.Empty;
        return true;
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

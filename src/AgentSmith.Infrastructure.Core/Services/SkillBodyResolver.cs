using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Lazy-resolves a skill's role-specific body section and inlines {{ref:&lt;Id&gt;}} placeholders
/// against the skill's references files. Cached per (skill name, role) tuple — references files
/// are read once per process lifetime.
/// </summary>
public sealed class SkillBodyResolver(ILogger<SkillBodyResolver> logger) : ISkillBodyResolver
{
    private static readonly Regex RefPlaceholder = new(
        @"\{\{ref:([a-zA-Z0-9_\-]+)\}\}",
        RegexOptions.Compiled);

    private readonly ConcurrentDictionary<(string Skill, SkillRole Role), string> _cache = new();

    public string ResolveBody(RoleSkillDefinition skill, SkillRole role)
    {
        return _cache.GetOrAdd((skill.Name, role), _ => Build(skill, role));
    }

    private string Build(RoleSkillDefinition skill, SkillRole role)
    {
        var bodySource = SelectRoleBody(skill, role);
        return RefPlaceholder.Replace(bodySource, m =>
        {
            var refId = m.Groups[1].Value;
            var reference = skill.References?.FirstOrDefault(r => r.Id == refId);
            if (reference is null)
            {
                logger.LogWarning(
                    "Skill '{Skill}' role '{Role}' body cites {{{{ref:{RefId}}}}} but no reference with that id is declared",
                    skill.Name, role, refId);
                return m.Value;
            }
            return ReadReference(skill, reference, m.Value);
        });
    }

    private static string SelectRoleBody(RoleSkillDefinition skill, SkillRole role)
    {
        if (skill.RoleBodies is not null && skill.RoleBodies.TryGetValue(role, out var section))
            return section;
        return skill.Rules;
    }

    private string ReadReference(RoleSkillDefinition skill, SkillReference reference, string fallback)
    {
        if (string.IsNullOrEmpty(skill.SkillDirectory))
        {
            logger.LogWarning(
                "Skill '{Skill}': cannot resolve {{{{ref:{Id}}}}} — SkillDirectory not set",
                skill.Name, reference.Id);
            return fallback;
        }
        var fullPath = Path.GetFullPath(Path.Combine(skill.SkillDirectory, reference.Path));
        if (!File.Exists(fullPath))
        {
            logger.LogWarning(
                "Skill '{Skill}': reference '{Id}' file not found at {Path}",
                skill.Name, reference.Id, fullPath);
            return fallback;
        }
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Skill '{Skill}': failed to read reference '{Id}' at {Path}",
                skill.Name, reference.Id, fullPath);
            return fallback;
        }
    }
}

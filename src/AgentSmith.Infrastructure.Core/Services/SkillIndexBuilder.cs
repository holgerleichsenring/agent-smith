using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Aggregates loaded skills into per-category index files at <c>&lt;skillsDir&gt;/_index/&lt;category&gt;.yaml</c>.
/// Triage reads these compact projections instead of the full SKILL.md set. Index is convenience —
/// read-only filesystems trigger a warning, not a crash.
/// </summary>
public sealed class SkillIndexBuilder(ILogger<SkillIndexBuilder> logger)
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public void Build(string skillsDirectory, IReadOnlyList<RoleSkillDefinition> skills)
    {
        var indexDir = Path.Combine(skillsDirectory, "_index");
        try
        {
            Directory.CreateDirectory(indexDir);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not create index directory at {Path} — skipping _index/ generation",
                indexDir);
            return;
        }

        var byCategory = new Dictionary<string, List<SkillIndexEntry>>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            if (skill.RolesSupported is null) continue; // legacy / pre-p0111 skill — not indexed
            var category = ExtractCategory(skill.SkillDirectory, skillsDirectory);
            if (category is null) continue;
            if (!byCategory.TryGetValue(category, out var list))
            {
                list = [];
                byCategory[category] = list;
            }
            list.Add(ToEntry(skill));
        }

        foreach (var (category, entries) in byCategory)
        {
            var path = Path.Combine(indexDir, $"{category}.yaml");
            try
            {
                var yaml = Serializer.Serialize(new
                {
                    skills = entries.Select(ToYamlShape).ToList()
                });
                File.WriteAllText(path, yaml);
                logger.LogInformation("Wrote skill index {Path} ({Count} skills)", path, entries.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not write skill index to {Path} — triage will fall back to in-memory aggregation",
                    path);
            }
        }
    }

    private static string? ExtractCategory(string? skillDirectory, string skillsRoot)
    {
        if (string.IsNullOrEmpty(skillDirectory)) return null;
        var rel = Path.GetRelativePath(skillsRoot, skillDirectory);
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 1 ? parts[0] : null;
    }

    private static SkillIndexEntry ToEntry(RoleSkillDefinition skill) =>
        new(
            skill.Name,
            skill.Description,
            skill.RolesSupported ?? [],
            skill.Activation ?? ActivationCriteria.Empty,
            skill.RoleAssignments ?? [],
            skill.OutputContract?.OutputType ?? new Dictionary<SkillRole, OutputForm>());

    /// <summary>
    /// YAML-friendly projection of <see cref="SkillIndexEntry"/> with role/output-form
    /// values lowered to match the frontmatter convention. The triage LLM reads role
    /// names in the same case as the source SKILL.md frontmatter, not C# PascalCase.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> ToYamlShape(SkillIndexEntry entry) =>
        new Dictionary<string, object?>
        {
            ["name"] = entry.Name,
            ["description"] = entry.Description,
            ["roles_supported"] = entry.RolesSupported.Select(r => r.ToString().ToLowerInvariant()).ToList(),
            ["activation"] = new Dictionary<string, object?>
            {
                ["positive"] = entry.Activation.Positive.Select(k => new { key = k.Key, desc = k.Desc }).ToList(),
                ["negative"] = entry.Activation.Negative.Select(k => new { key = k.Key, desc = k.Desc }).ToList()
            },
            ["role_assignment"] = entry.RoleAssignments.ToDictionary(
                ra => ra.Role.ToString().ToLowerInvariant(),
                ra => (object)new Dictionary<string, object>
                {
                    ["positive"] = ra.Criteria.Positive.Select(k => new { key = k.Key, desc = k.Desc }).ToList(),
                    ["negative"] = ra.Criteria.Negative.Select(k => new { key = k.Key, desc = k.Desc }).ToList()
                }),
            ["output_type"] = entry.OutputType.ToDictionary(
                kv => kv.Key.ToString().ToLowerInvariant(),
                kv => kv.Value.ToString().ToLowerInvariant())
        };
}

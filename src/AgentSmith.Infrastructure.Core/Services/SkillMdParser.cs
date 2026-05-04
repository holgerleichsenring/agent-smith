using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Parses SKILL.md + agentsmith.md + source.md files into a RoleSkillDefinition.
/// Reads p0111 extended frontmatter (roles_supported, activation, role_assignment, references,
/// output_contract) and splits the body into per-role sections.
/// Honors per-provider SKILL.&lt;provider&gt;.md overrides via IProviderOverrideResolver.
/// </summary>
internal sealed class SkillMdParser(IProviderOverrideResolver overrideResolver, ILogger logger)
{
    private static readonly IDeserializer FrontmatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal RoleSkillDefinition? Parse(string skillDirectory)
    {
        var paths = overrideResolver.Resolve(skillDirectory);
        var role = paths.BasePath is null
            ? BuildFromFile(paths.EffectivePath, skillDirectory)
            : BuildFromOverride(paths, skillDirectory);
        if (role is null) return null;

        LoadAgentSmithExtensions(skillDirectory, role);
        LoadSource(skillDirectory, role);
        return role;
    }

    private RoleSkillDefinition? BuildFromFile(string skillMdPath, string skillDirectory)
    {
        var (meta, body) = ReadFrontmatterAndBody(skillMdPath);
        return meta is null ? null : BuildRole(meta, body, skillDirectory, skillMdPath);
    }

    private RoleSkillDefinition? BuildFromOverride(ProviderOverridePaths paths, string skillDirectory)
    {
        var (baseMeta, _) = ReadFrontmatterAndBody(paths.BasePath!);
        var (overMeta, overBody) = ReadFrontmatterAndBody(paths.EffectivePath);
        if (baseMeta is null)
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' loaded but base SKILL.md at '{paths.BasePath}' is missing or invalid.");
        if (overMeta is null) return null;

        ValidateOverrideMatchesBase(overMeta, baseMeta, paths);
        var merged = MergeFrontmatter(baseMeta, overMeta);
        var role = BuildRole(merged, overBody, skillDirectory, paths.EffectivePath);
        if (role is not null)
            logger.LogInformation(
                "Provider override loaded for skill '{Skill}' from {Path}", role.Name, paths.EffectivePath);
        return role;
    }

    private (SkillMdFrontmatter? Meta, string Body) ReadFrontmatterAndBody(string skillMdPath)
    {
        var content = File.ReadAllText(skillMdPath);
        var (frontmatter, body) = ParseFrontmatter(content);
        if (string.IsNullOrWhiteSpace(frontmatter))
        {
            logger.LogWarning("No frontmatter found in {Path}", skillMdPath);
            return (null, body);
        }
        var meta = FrontmatterDeserializer.Deserialize<SkillMdFrontmatter>(frontmatter);
        if (meta is null || string.IsNullOrEmpty(meta.Name)) return (null, body);
        return (meta, body);
    }

    private RoleSkillDefinition BuildRole(
        SkillMdFrontmatter meta, string body, string skillDirectory, string skillMdPath) =>
        new()
        {
            Name = meta.Name,
            DisplayName = meta.DisplayName ?? string.Empty,
            Emoji = meta.Emoji ?? string.Empty,
            Description = meta.Description ?? string.Empty,
            Triggers = meta.Triggers ?? [],
            Rules = body.Trim(),
            SkillDirectory = skillDirectory,
            RolesSupported = MapRolesSupported(meta.RolesSupported, skillMdPath),
            Activation = MapActivation(meta.Activation),
            RoleAssignments = MapRoleAssignments(meta.RoleAssignment, skillMdPath),
            References = MapReferences(meta.References),
            OutputContract = MapOutputContract(meta.OutputContract, skillMdPath),
            RoleBodies = SkillBodySplitter.Split(body)
        };

    private static void ValidateOverrideMatchesBase(
        SkillMdFrontmatter over, SkillMdFrontmatter @base, ProviderOverridePaths paths)
    {
        if (!string.Equals(over.Name, @base.Name, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' has name='{over.Name}' but base SKILL.md has name='{@base.Name}'. Names must match.");

        if (over.RolesSupported is null)
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' must declare roles_supported; cannot inherit it from base.");

        var overSet = over.RolesSupported.ToHashSet();
        var baseSet = (@base.RolesSupported ?? []).ToHashSet();
        if (!overSet.SetEquals(baseSet))
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' has roles_supported=[{string.Join(",", overSet)}] " +
                $"but base SKILL.md has roles_supported=[{string.Join(",", baseSet)}]. They must match.");
    }

    private static SkillMdFrontmatter MergeFrontmatter(
        SkillMdFrontmatter @base, SkillMdFrontmatter over) => new()
        {
            Name = over.Name,
            DisplayName = over.DisplayName ?? @base.DisplayName,
            Emoji = over.Emoji ?? @base.Emoji,
            Description = over.Description ?? @base.Description,
            Triggers = over.Triggers ?? @base.Triggers,
            Version = over.Version ?? @base.Version,
            AllowedTools = over.AllowedTools ?? @base.AllowedTools,
            RolesSupported = over.RolesSupported,
            Activation = over.Activation ?? @base.Activation,
            RoleAssignment = over.RoleAssignment ?? @base.RoleAssignment,
            References = over.References ?? @base.References,
            OutputContract = over.OutputContract ?? @base.OutputContract,
        };

    private IReadOnlyList<SkillRole>? MapRolesSupported(List<string>? raw, string skillMdPath)
    {
        if (raw is null) return null;
        var result = new List<SkillRole>(raw.Count);
        foreach (var s in raw)
        {
            if (TryParseRole(s, out var role))
            {
                result.Add(role);
            }
            else
            {
                logger.LogError("Skill {Path}: unknown role '{Role}' in roles_supported", skillMdPath, s);
            }
        }
        return result;
    }

    private static ActivationCriteria? MapActivation(RawActivationCriteria? raw)
    {
        if (raw is null) return null;
        return new ActivationCriteria(
            (raw.Positive ?? []).Select(k => new ActivationKey(k.Key, k.Desc)).ToList(),
            (raw.Negative ?? []).Select(k => new ActivationKey(k.Key, k.Desc)).ToList());
    }

    private IReadOnlyList<RoleAssignment>? MapRoleAssignments(
        Dictionary<string, RawActivationCriteria>? raw,
        string skillMdPath)
    {
        if (raw is null || raw.Count == 0) return null;
        var result = new List<RoleAssignment>(raw.Count);
        foreach (var (roleName, criteria) in raw)
        {
            if (!TryParseRole(roleName, out var role))
            {
                logger.LogError("Skill {Path}: unknown role '{Role}' in role_assignment", skillMdPath, roleName);
                continue;
            }
            var mapped = MapActivation(criteria) ?? ActivationCriteria.Empty;
            result.Add(new RoleAssignment(role, mapped));
        }
        return result;
    }

    private static IReadOnlyList<SkillReference>? MapReferences(List<RawSkillReference>? raw)
    {
        if (raw is null) return null;
        return raw.Select(r => new SkillReference(r.Id, r.Path)).ToList();
    }

    private OutputContract? MapOutputContract(RawOutputContract? raw, string skillMdPath)
    {
        if (raw is null) return null;
        var outputType = new Dictionary<SkillRole, OutputForm>();
        if (raw.OutputType is not null)
        {
            foreach (var (roleName, formName) in raw.OutputType)
            {
                if (!TryParseRole(roleName, out var role))
                {
                    logger.LogError(
                        "Skill {Path}: unknown role '{Role}' in output_contract.output_type",
                        skillMdPath, roleName);
                    continue;
                }
                if (!TryParseOutputForm(formName, out var form))
                {
                    logger.LogError(
                        "Skill {Path}: unknown output form '{Form}' in output_contract.output_type",
                        skillMdPath, formName);
                    continue;
                }
                outputType[role] = form;
            }
        }
        return new OutputContract(
            raw.SchemaRef ?? string.Empty,
            raw.HardLimits?.MaxObservations ?? 0,
            raw.HardLimits?.MaxCharsPerField ?? 0,
            outputType);
    }

    private static bool TryParseRole(string raw, out SkillRole role)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "lead": role = SkillRole.Lead; return true;
            case "analyst": role = SkillRole.Analyst; return true;
            case "reviewer": role = SkillRole.Reviewer; return true;
            case "filter": role = SkillRole.Filter; return true;
            default: role = default; return false;
        }
    }

    private static bool TryParseOutputForm(string raw, out OutputForm form)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "list": form = OutputForm.List; return true;
            case "plan": form = OutputForm.Plan; return true;
            case "artifact": form = OutputForm.Artifact; return true;
            default: form = default; return false;
        }
    }

    private static void LoadAgentSmithExtensions(string skillDirectory, RoleSkillDefinition role)
    {
        var path = Path.Combine(skillDirectory, "agentsmith.md");
        if (!File.Exists(path)) return;

        var content = File.ReadAllText(path);
        role.ConvergenceCriteria = MarkdownSectionParser.ParseListSection(content, "convergence_criteria");

        var displayName = MarkdownSectionParser.ParseSingleField(content, "display-name");
        if (!string.IsNullOrEmpty(displayName))
            role.DisplayName = displayName;

        var emoji = MarkdownSectionParser.ParseSingleField(content, "emoji");
        if (!string.IsNullOrEmpty(emoji))
            role.Emoji = emoji;

        var triggers = MarkdownSectionParser.ParseListSection(content, "triggers");
        if (triggers.Count > 0)
            role.Triggers = triggers;
    }

    private void LoadSource(string skillDirectory, RoleSkillDefinition role)
    {
        var path = Path.Combine(skillDirectory, "source.md");
        if (!File.Exists(path)) return;

        try
        {
            var content = File.ReadAllText(path);
            var origin = ExtractField(content, "origin");
            var version = ExtractField(content, "version");
            var commit = ExtractField(content, "commit");
            var reviewedStr = ExtractField(content, "reviewed");
            var reviewedBy = ExtractField(content, "reviewed-by");

            if (string.IsNullOrEmpty(origin)) return;

            var reviewed = DateOnly.TryParse(reviewedStr, out var date) ? date : DateOnly.MinValue;
            role.Source = new SkillSource(origin, version, commit, reviewed, reviewedBy);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse source.md from {Path}", path);
        }
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

    private static string ExtractField(string content, string fieldName)
    {
        var pattern = $@"^{Regex.Escape(fieldName)}:\s*(.+)$";
        var match = Regex.Match(content, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}

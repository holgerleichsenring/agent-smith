using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Exceptions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Parses SKILL.md + agentsmith.md + source.md files into a RoleSkillDefinition.
/// p0127c: only the new single-body format is accepted; legacy roles_supported shape
/// throws SkillFormatException with a migration message. Honors per-provider
/// SKILL.&lt;provider&gt;.md overrides via IProviderOverrideResolver.
/// </summary>
internal sealed class SkillMdParser(IProviderOverrideResolver overrideResolver, ILogger logger)
{
    private static readonly IDeserializer FrontmatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly NewFormatSkillBuilder _newFormatBuilder = new(new NewFormatSkillValidator());

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
        SkillMdFrontmatter meta, string body, string skillDirectory, string skillMdPath)
    {
        if (meta.RolesSupported is not null)
            throw new SkillFormatException(
                skillMdPath,
                "legacy 'roles_supported' field is no longer accepted; migrate to the single-body 'role' format introduced in agent-smith-skills 2.0.0");
        if (string.IsNullOrWhiteSpace(meta.Role))
            throw new SkillFormatException(
                skillMdPath,
                "missing required field 'role' (new SKILL.md format introduced in agent-smith-skills 2.0.0)");
        return _newFormatBuilder.Build(meta, body, skillDirectory, skillMdPath);
    }

    private static void ValidateOverrideMatchesBase(
        SkillMdFrontmatter over, SkillMdFrontmatter @base, ProviderOverridePaths paths)
    {
        if (!string.Equals(over.Name, @base.Name, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' has name='{over.Name}' but base SKILL.md has name='{@base.Name}'. Names must match.");

        if (over.Role is not null && !string.Equals(over.Role, @base.Role, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Provider override at '{paths.EffectivePath}' has role='{over.Role}' but base SKILL.md has role='{@base.Role}'. Roles must match.");
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
            ActivatesWhen = over.ActivatesWhen ?? @base.ActivatesWhen,
            Role = over.Role ?? @base.Role,
            Category = over.Category ?? @base.Category,
            InvestigatorMode = over.InvestigatorMode ?? @base.InvestigatorMode,
            SurveyScope = over.SurveyScope ?? @base.SurveyScope,
            ScopeHint = over.ScopeHint ?? @base.ScopeHint,
            BlockCondition = over.BlockCondition ?? @base.BlockCondition,
            Loop = over.Loop ?? @base.Loop,
            OutputSchema = over.OutputSchema ?? @base.OutputSchema,
        };

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

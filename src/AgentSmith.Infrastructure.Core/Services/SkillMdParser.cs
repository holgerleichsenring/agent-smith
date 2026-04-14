using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Parses SKILL.md + agentsmith.md + source.md files into a RoleSkillDefinition.
/// </summary>
internal sealed class SkillMdParser(ILogger logger)
{
    private static readonly IDeserializer FrontmatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal RoleSkillDefinition? Parse(string skillDirectory)
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

        LoadAgentSmithExtensions(skillDirectory, role);
        LoadSource(skillDirectory, role);

        return role;
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

        role.Orchestration = SkillOrchestrationParser.Parse(content);
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

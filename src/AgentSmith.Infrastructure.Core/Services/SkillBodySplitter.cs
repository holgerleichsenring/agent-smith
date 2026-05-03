using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Splits a SKILL.md body string by ## as_lead / ## as_analyst / ## as_reviewer / ## as_filter
/// headers into a per-role body dictionary. Skills without role headers return null (caller
/// falls back to the full body string for any role).
/// </summary>
internal static class SkillBodySplitter
{
    private static readonly Regex SectionHeader = new(
        @"^##\s+as_(lead|analyst|reviewer|filter)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyDictionary<SkillRole, string>? Split(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        var matches = SectionHeader.Matches(body);
        if (matches.Count == 0) return null;

        var result = new Dictionary<SkillRole, string>();
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var role = ParseRole(match.Groups[1].Value);
            if (role is null) continue;

            var sectionStart = match.Index + match.Length;
            var sectionEnd = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var sectionBody = body[sectionStart..sectionEnd].Trim();

            result[role.Value] = sectionBody;
        }

        return result.Count > 0 ? result : null;
    }

    private static SkillRole? ParseRole(string raw) => raw.ToLowerInvariant() switch
    {
        "lead" => SkillRole.Lead,
        "analyst" => SkillRole.Analyst,
        "reviewer" => SkillRole.Reviewer,
        "filter" => SkillRole.Filter,
        _ => null
    };
}

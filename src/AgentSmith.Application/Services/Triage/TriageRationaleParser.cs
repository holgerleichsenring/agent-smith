using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Parses the rationale string of a TriageOutput into structured RationaleEntry records.
/// Token grammar: "<role>=<skill>:<key>;" (positive) and "-<skill>:<key>;" (negative, role-less).
/// Tokens are separated by ';'. Whitespace is tolerated. Malformed tokens are silently dropped —
/// validation that catches missing keys lives in TriageOutputValidator.
/// </summary>
public sealed class TriageRationaleParser
{
    private static readonly Regex PositivePattern = new(
        @"^(?<role>lead|analyst|reviewer|filter)=(?<skill>[\w\-]+):(?<key>[\w\-]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NegativePattern = new(
        @"^-(?<skill>[\w\-]+):(?<key>[\w\-]+)$",
        RegexOptions.Compiled);

    public IReadOnlyList<RationaleEntry> Parse(string rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
            return Array.Empty<RationaleEntry>();

        var entries = new List<RationaleEntry>();
        var tokens = rationale.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var entry = ParseToken(token);
            if (entry is not null) entries.Add(entry);
        }
        return entries;
    }

    private static RationaleEntry? ParseToken(string token)
    {
        var positive = PositivePattern.Match(token);
        if (positive.Success)
        {
            var role = ParseRole(positive.Groups["role"].Value);
            if (role is null) return null;
            return new RationaleEntry(role, positive.Groups["skill"].Value, positive.Groups["key"].Value, Negative: false);
        }
        var negative = NegativePattern.Match(token);
        if (negative.Success)
            return new RationaleEntry(Role: null, negative.Groups["skill"].Value, negative.Groups["key"].Value, Negative: true);
        return null;
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

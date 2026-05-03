using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Parses the ## orchestration section from agentsmith.md into a SkillOrchestration.
/// Gate-role skills must declare input_categories explicitly: either "*" for all
/// categories, or a non-empty list. Empty or missing input_categories is rejected
/// because the empty-means-everything ambiguity hides authoring mistakes.
/// </summary>
internal static class SkillOrchestrationParser
{
    internal const string Wildcard = "*";

    internal static SkillOrchestration? Parse(string content)
    {
        var inSection = false;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("## orchestration", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection && trimmed.StartsWith("## "))
                break;

            if (inSection && trimmed.Contains(':'))
            {
                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed[..colonIndex].Trim();
                var value = trimmed[(colonIndex + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    fields[key] = value;
            }
        }

        if (fields.Count == 0)
            return null;

        if (!fields.TryGetValue("role", out var roleStr) ||
            !Enum.TryParse<OrchestrationRole>(roleStr, ignoreCase: true, out var role))
            role = OrchestrationRole.Contributor;

        if (!fields.TryGetValue("output", out var outputStr) ||
            !Enum.TryParse<SkillOutputType>(outputStr, ignoreCase: true, out var output))
            output = SkillOutputType.Artifact;

        var inputCategories = ParseCommaSeparated(fields.GetValueOrDefault("input_categories", ""));

        if (role == OrchestrationRole.Gate && output == SkillOutputType.List)
            ValidateGateListInputCategories(inputCategories);

        return new SkillOrchestration(
            role,
            output,
            ParseCommaSeparated(fields.GetValueOrDefault("runs_after", "")),
            ParseCommaSeparated(fields.GetValueOrDefault("runs_before", "")),
            ParseCommaSeparated(fields.GetValueOrDefault("parallel_with", "")),
            inputCategories);
    }

    private static void ValidateGateListInputCategories(IReadOnlyList<string> categories)
    {
        if (categories.Count == 0)
            throw new InvalidOperationException(
                "Gate (output: list) skills must declare input_categories: use '*' for all categories, " +
                "or a comma-separated list of explicit categories. Empty is rejected.");

        if (categories.Count > 1 && categories.Any(c => c == Wildcard))
            throw new InvalidOperationException(
                "Gate input_categories cannot mix '*' wildcard with explicit categories. " +
                "Use either '*' alone or an explicit list.");
    }

    private static IReadOnlyList<string> ParseCommaSeparated(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}

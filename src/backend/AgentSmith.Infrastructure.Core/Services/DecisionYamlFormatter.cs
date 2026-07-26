using AgentSmith.Contracts.Decisions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0380: renders one decision as a decision.schema.json-conformant YAML list
/// item ({category, chose}). Double-quoted scalar so arbitrary decision text
/// (colons, quotes, newlines) stays valid YAML.
/// </summary>
internal static class DecisionYamlFormatter
{
    public static string FormatItem(DecisionCategory category, string decision) =>
        $"  - category: {category}\n    chose: {Quote(decision)}\n";

    private static string Quote(string value) =>
        "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n")
        + "\"";
}

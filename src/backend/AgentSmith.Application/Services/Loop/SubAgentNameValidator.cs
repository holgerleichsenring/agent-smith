using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: rejects generic sub-agent names that would rot the dashboard's
/// identity story. The validator is the single source of truth for what
/// counts as "generic" — SpawnAgentToolHost calls it before any LLM cost
/// is incurred, and the tests share the same instance so the regex is
/// not duplicated.
/// </summary>
public sealed class SubAgentNameValidator
{
    private static readonly Regex AgentDigits = new(@"^agent\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SubDigits = new(@"^sub\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ChildDigits = new(@"^child\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> SingleWordGenerics = new(StringComparer.OrdinalIgnoreCase)
    {
        "worker", "helper", "runner", "executor", "processor",
        "agent", "sub", "child", "task", "job",
    };

    public bool IsValid(string? name) => Reject(name) is null;

    /// <summary>
    /// Returns a human-readable rejection reason or null when the name passes.
    /// </summary>
    public string? Reject(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "name must not be empty";
        var trimmed = name.Trim();
        if (AgentDigits.IsMatch(trimmed)) return $"name '{trimmed}' is generic (^agent\\d+$)";
        if (SubDigits.IsMatch(trimmed)) return $"name '{trimmed}' is generic (^sub\\d+$)";
        if (ChildDigits.IsMatch(trimmed)) return $"name '{trimmed}' is generic (^child\\d+$)";
        if (SingleWordGenerics.Contains(trimmed)) return $"name '{trimmed}' is a single-word generic";
        return null;
    }
}

using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863e: a pipeline input's placeholders — {version}, {environment}, {context} and the template's declared
/// parameters. The template is checked when it is read (every name known, {version} carried somewhere); the deploy fills
/// them, and a declared parameter with neither a bound value nor a default is refused, never sent empty.
/// </summary>
public static partial class PipelineInputValues
{
    public const string Version = "version";
    public const string Environment = "environment";
    public const string Context = "context";
    public static IReadOnlyList<string> Given => [Version, Environment, Context];

    /// <summary>Why the inputs cannot be filled from what a deploy knows, or null.</summary>
    public static string? Check(string environment, IReadOnlyDictionary<string, string> inputs, IEnumerable<string> parameters)
    {
        var known = Given.Concat(parameters).ToHashSet(StringComparer.Ordinal);
        var named = inputs.Values.SelectMany(Names).ToList();
        if (named.FirstOrDefault(n => !known.Contains(n)) is { } unknown)
            return $"environment '{environment}' inputs name {{{unknown}}}, which is neither {string.Join(", ", Given.Select(g => $"{{{g}}}"))} nor a declared parameter";
        return named.Contains(Version) ? null
            : $"environment '{environment}' passes the version in none of its inputs; give one the value {{{Version}}}";
    }

    /// <summary>The inputs with every placeholder replaced, or null with the placeholder that has no value.</summary>
    public static (IReadOnlyDictionary<string, string>? Inputs, string? Error) Fill(
        IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string?> values)
    {
        if (inputs.Values.SelectMany(Names).FirstOrDefault(n => values.GetValueOrDefault(n) is null) is { } missing)
            return (null, $"parameter '{missing}' has no value in the binding and no default in the template");
        return (inputs.ToDictionary(i => i.Key, i => Placeholder().Replace(i.Value, m => values[m.Groups[1].Value]!)), null);
    }

    private static IEnumerable<string> Names(string value) => Placeholder().Matches(value).Select(m => m.Groups[1].Value);

    [GeneratedRegex(@"\{([A-Za-z0-9_.-]+)\}")]
    private static partial Regex Placeholder();
}

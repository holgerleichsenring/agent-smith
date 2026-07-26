using System.Text.RegularExpressions;

namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// The placeholder convention handed to the LLM in place of a real registry
/// token: <c>__AS_TOKEN_&lt;host&gt;__</c>. The LLM emits this token literal; the
/// host substitutes the real secret matched by host just before writing. Pure
/// format/parse helpers only — the secret never passes through here.
/// </summary>
public static partial class RegistryTokenPlaceholder
{
    public const string Prefix = "__AS_TOKEN_";
    public const string Suffix = "__";

    public static string For(string host) => Prefix + host + Suffix;

    /// <summary>Distinct hosts referenced by every placeholder occurrence in <paramref name="content"/>.</summary>
    public static IReadOnlyList<string> HostsIn(string content) =>
        Pattern().Matches(content)
            .Select(m => m.Groups["host"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string Replace(string content, Func<string, string?> tokenForHost) =>
        Pattern().Replace(content, m =>
        {
            var host = m.Groups["host"].Value;
            return tokenForHost(host) ?? m.Value;
        });

    // Host segment is a DNS hostname: letters, digits, dots and hyphens.
    [GeneratedRegex(@"__AS_TOKEN_(?<host>[A-Za-z0-9.\-]+)__", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: matches a discovered repo name against a glob pattern where <c>*</c> is the only
/// wildcard (matches any run of characters). Case-insensitive. A pattern with no <c>*</c> is
/// an exact (case-insensitive) name match.
/// </summary>
public static class RepoGlobMatcher
{
    public static bool IsMatch(string pattern, string repoName)
    {
        if (!pattern.Contains('*'))
            return string.Equals(pattern, repoName, StringComparison.OrdinalIgnoreCase);

        var regex = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
        return Regex.IsMatch(repoName, regex, RegexOptions.IgnoreCase);
    }
}

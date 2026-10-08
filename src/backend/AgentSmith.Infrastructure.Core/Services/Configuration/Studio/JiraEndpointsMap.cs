using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// A tracker's Jira <c>endpoints:</c> block as the studio edits it: a map of the paths that
/// differ from the Jira Cloud v3 defaults. Only the overrides travel, so a saved tracker keeps
/// following a default it never changed.
/// </summary>
internal static class JiraEndpointsMap
{
    private static readonly JiraEndpoints Defaults = new();

    private static readonly (string Key, Func<JiraEndpoints, string> Get,
        Func<JiraEndpoints, string, JiraEndpoints> With)[] Paths =
    [
        ("search", e => e.Search, (e, v) => e with { Search = v }),
        ("issue", e => e.Issue, (e, v) => e with { Issue = v }),
        ("comment", e => e.Comment, (e, v) => e with { Comment = v }),
        ("transitions", e => e.Transitions, (e, v) => e with { Transitions = v }),
        ("create", e => e.Create, (e, v) => e with { Create = v }),
        ("changelog", e => e.Changelog, (e, v) => e with { Changelog = v }), // 2026-10-08-2123
        ("myself", e => e.Myself, (e, v) => e with { Myself = v }),
    ];

    /// <summary>The overridden paths, or null when every path is the default.</summary>
    public static IReadOnlyDictionary<string, string>? Overrides(JiraEndpoints? endpoints)
    {
        if (endpoints is null) return null;
        var overrides = Paths.Where(p => p.Get(endpoints) != p.Get(Defaults))
            .ToDictionary(p => p.Key, p => p.Get(endpoints));
        return overrides.Count > 0 ? overrides : null;
    }

    /// <summary>The endpoints block the overrides describe; an empty map is no block at all.</summary>
    public static JiraEndpoints? From(IReadOnlyDictionary<string, string> overrides)
    {
        var result = new JiraEndpoints();
        foreach (var (key, value) in overrides.Where(o => !string.IsNullOrWhiteSpace(o.Value)))
        {
            var path = Paths.FirstOrDefault(p => p.Key == key);
            if (path.Key is null)
                throw new ConfigurationException(
                    $"Unknown Jira endpoint '{key}' (known: {string.Join(", ", Paths.Select(p => p.Key))}).");
            result = path.With(result, value.Trim());
        }
        return result == Defaults ? null : result;
    }
}

using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-12-3a36e: the repositories a design dialog's specs name — a lone spec and every
/// child of an epic; the epic's parent is the umbrella ticket and changes nothing itself.
/// Decided by <see cref="SpecReposRule"/> against the turn's scope: an out-of-scope name is
/// refused for the one re-prompt, a draft that names none is filled. The dialog declares no
/// contexts, so only the scope is checked.
/// </summary>
public sealed class DialogDraftRepos(SpecReposRule rule, PhaseDraftReposWriter writer)
{
    /// <summary>The lone spec's text with its repos decided, or why the reply is refused.</summary>
    public (string? Yaml, string? Error) ForSpec(string yaml, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0) return (yaml, null);
        var stated = PhaseDraftLists.Strings(OutcomeYamlReader.ReadMap(yaml), "repos");
        var resolution = rule.Resolve([stated], scope);
        if (resolution.Error is not null) return (null, resolution.Error);
        return resolution.Repos[0].SequenceEqual(stated, StringComparer.Ordinal)
            ? (yaml, null)
            : (writer.Write(yaml, resolution.Repos[0]), null);
    }

    /// <summary>Writes each child's decided repos into its mapping; the reason the epic is
    /// refused, or null.</summary>
    public string? ForChildren(IReadOnlyList<Dictionary<object, object?>> children, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0) return null;
        var stated = children
            .Select(c => PhaseDraftLists.Strings(c.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => e.Value), "repos"))
            .ToList();
        var resolution = rule.Resolve(stated, scope);
        if (resolution.Error is not null) return $"epic children: {resolution.Error}";
        for (var i = 0; i < children.Count; i++)
            children[i]["repos"] = resolution.Repos[i].ToList();
        return null;
    }
}

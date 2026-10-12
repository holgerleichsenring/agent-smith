namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: decides the repositories every spec a derivation or design dialog cuts
/// names — the field placement reads.
/// <para>
/// ARMED BY THE REPLY, like <see cref="ScopedContextCoverage"/>: when any spec states
/// <c>repos</c>, every spec must, and each name must be one of the scope's. A reply that
/// states none is filled with the whole scope, so a binary ahead of the skills release, or
/// an operator's override catalog, still cuts. One repository in scope is filled and never
/// re-asked: there is nothing for the model to decide.
/// </para>
/// </summary>
public sealed class SpecReposRule
{
    /// <param name="stated">What each spec stated, in order; an empty entry stated nothing.</param>
    /// <param name="scope">The scope's repository names; empty when no scope is known.</param>
    public SpecReposResolution Resolve(IReadOnlyList<IReadOnlyList<string>> stated, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(stated);
        ArgumentNullException.ThrowIfNull(scope);
        var armed = stated.Any(s => s.Count > 0);
        if (scope.Count == 0) return new SpecReposResolution(stated, armed, null);
        if (scope.Count == 1 || !armed)
            return new SpecReposResolution([.. stated.Select(_ => scope)], false, null);
        return Checked(stated, scope);
    }

    /// <summary>The first <c>repo/context</c> a spec declares for a repository its own
    /// <c>repos</c> does not name, as the reason to ask again; null when they agree.</summary>
    public string? ContextDisagreement(IReadOnlyList<string> repos, IReadOnlyList<string> contexts, int position)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(contexts);
        foreach (var context in contexts)
        {
            var slash = context.IndexOf('/');
            if (slash <= 0) continue;
            var repo = context[..slash];
            if (!repos.Any(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase)))
                return $"spec {position} declares context '{context}' but its repos does not name '{repo}' — "
                    + "a spec that changes a repository's context changes that repository, so name it in repos";
        }
        return null;
    }

    private static SpecReposResolution Checked(IReadOnlyList<IReadOnlyList<string>> stated, IReadOnlyList<string> scope)
    {
        var allowed = string.Join(", ", scope);
        for (var i = 0; i < stated.Count; i++)
        {
            if (stated[i].Count == 0)
                return SpecReposResolution.Refused(
                    $"spec {i + 1} names no repository while another spec does — when one spec names its "
                    + $"repositories in repos, every spec must, from: {allowed}");
            if (stated[i].FirstOrDefault(r => InScope(r, scope) is null) is { } outside)
                return SpecReposResolution.Refused(
                    $"spec {i + 1} names repository '{outside}' in repos, which is not in scope — name only: {allowed}");
        }
        return new SpecReposResolution(
            [.. stated.Select(s => (IReadOnlyList<string>)[.. s.Select(r => InScope(r, scope)!).Distinct()])],
            true, null);
    }

    // The scope's own spelling of a stated name, so a case slip neither refuses nor survives.
    private static string? InScope(string name, IReadOnlyList<string> scope) =>
        scope.FirstOrDefault(s => string.Equals(s, name.Trim(), StringComparison.OrdinalIgnoreCase));
}
